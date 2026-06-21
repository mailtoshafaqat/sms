using System.Text.Json;
using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Services;

public class LocalBiometricService(
    ILocalBiometricRepository localBiometricRepository,
    IStudentRepository studentRepository,
    IBiometricDeviceRepository biometricDeviceRepository,
    IAttendanceService attendanceService,
    IUnitOfWork unitOfWork) : ILocalBiometricService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<string> EnrollFaceAsync(int studentId, IReadOnlyList<float> descriptor, CancellationToken cancellationToken = default)
    {
        ValidateDescriptor(descriptor);

        _ = await studentRepository.GetStudentByIdAsync(studentId, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Student not found.");

        var externalId = BuildExternalId(BiometricType.Face, studentId);
        var existing = await localBiometricRepository.GetTemplateAsync(studentId, BiometricType.Face, cancellationToken: cancellationToken);
        var descriptors = existing is null
            ? new List<float[]>()
            : DeserializeDescriptorSet(existing.TemplateData).ToList();

        descriptors.Add(descriptor.ToArray());
        if (descriptors.Count > 3)
        {
            descriptors.RemoveAt(0);
        }

        await SaveTemplateAsync(studentId, BiometricType.Face, externalId, descriptors, cancellationToken);
        await EnsureBiometricMapAsync(studentId, BiometricType.Face, externalId, cancellationToken);

        return externalId;
    }

    public async Task<LocalBiometricMatchDto?> MatchFaceAsync(IReadOnlyList<float> descriptor, CancellationToken cancellationToken = default)
    {
        ValidateDescriptor(descriptor);

        var templates = await localBiometricRepository.GetFaceTemplatesAsync(cancellationToken);
        StudentLocalTemplate? bestTemplate = null;
        var bestDistance = float.MaxValue;

        foreach (var template in templates)
        {
            foreach (var stored in DeserializeDescriptorSet(template.TemplateData))
            {
                var distance = FaceMatcher.Distance(descriptor, stored);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTemplate = template;
                }
            }
        }

        if (bestTemplate is null || bestDistance > FaceMatcher.GateMatchThreshold)
        {
            return null;
        }

        var student = await studentRepository.GetStudentByIdAsync(bestTemplate.StudentId, cancellationToken: cancellationToken);
        return new LocalBiometricMatchDto(
            bestTemplate.StudentId,
            student?.FullName ?? "Unknown",
            bestTemplate.ExternalId,
            bestDistance);
    }

    public async Task<string> EnrollFingerprintAsync(int studentId, string credentialId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            throw new InvalidOperationException("Credential id is required.");
        }

        _ = await studentRepository.GetStudentByIdAsync(studentId, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Student not found.");

        var externalId = BuildExternalId(BiometricType.Fingerprint, studentId);
        var payload = JsonSerializer.Serialize(new { credentialId = credentialId.Trim() }, JsonOptions);
        await SaveTemplateAsync(studentId, BiometricType.Fingerprint, externalId, payload, cancellationToken);
        await EnsureBiometricMapAsync(studentId, BiometricType.Fingerprint, externalId, cancellationToken);

        return externalId;
    }

    public async Task<LocalBiometricMatchDto?> MatchFingerprintAsync(string credentialId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            return null;
        }

        var templates = await localBiometricRepository.GetTemplatesByTypeAsync(BiometricType.Fingerprint, cancellationToken);
        var match = templates.FirstOrDefault(x => ReadCredentialId(x.TemplateData) == credentialId.Trim());
        if (match is null)
        {
            return null;
        }

        var student = await studentRepository.GetStudentByIdAsync(match.StudentId, cancellationToken: cancellationToken);
        return new LocalBiometricMatchDto(
            match.StudentId,
            student?.FullName ?? "Unknown",
            match.ExternalId,
            0f);
    }

    public async Task<LocalBiometricScanResultDto> ScanAsync(
        int studentId,
        BiometricType type,
        ScanDirection? direction = null,
        CancellationToken cancellationToken = default)
    {
        var template = await localBiometricRepository.GetTemplateAsync(studentId, type, cancellationToken: cancellationToken);
        if (template is null)
        {
            return new LocalBiometricScanResultDto(false, "No local enrollment found for this student.");
        }

        return await ScanByExternalIdAsync(template.ExternalId, type, direction, cancellationToken);
    }

    public async Task<LocalBiometricScanResultDto> ScanByExternalIdAsync(
        string externalId,
        BiometricType type,
        ScanDirection? direction = null,
        CancellationToken cancellationToken = default)
    {
        var device = await biometricDeviceRepository.GetEnabledDeviceForTypeAsync(type, cancellationToken: cancellationToken);
        if (device is null)
        {
            return new LocalBiometricScanResultDto(false, $"No enabled {BiometricTypeRules.GetDisplayName(type)} device configured.");
        }

        var template = await localBiometricRepository.GetByExternalIdAsync(externalId, cancellationToken: cancellationToken);
        if (template is not null)
        {
            await EnsureBiometricMapAsync(template.StudentId, type, externalId, cancellationToken);
        }

        var recorded = await attendanceService.ProcessBiometricScanAsync(externalId, device.Id, direction, cancellationToken);
        if (!recorded)
        {
            return new LocalBiometricScanResultDto(
                false,
                template is null
                    ? "Scan not recorded. Student is not enrolled for gate attendance."
                    : "Scan not recorded. Student may not be linked to the gate device, or scan was blocked as a duplicate (wait a few seconds).");
        }

        var student = template is null
            ? null
            : await studentRepository.GetStudentByIdAsync(template.StudentId, cancellationToken: cancellationToken);

        var directionLabel = direction?.ToString() ?? "AUTO (first scan IN, second OUT)";
        return new LocalBiometricScanResultDto(
            true,
            $"{BiometricTypeRules.GetDisplayName(type)} attendance marked ({directionLabel}).",
            template is null
                ? null
                : new LocalBiometricMatchDto(template.StudentId, student?.FullName ?? "Unknown", externalId, 0f));
    }

    public async Task<IReadOnlyList<GateFaceEnrollmentDto>> GetFaceEnrollmentsAsync(CancellationToken cancellationToken = default)
    {
        var templates = await localBiometricRepository.GetFaceTemplatesAsync(cancellationToken);
        var enrollments = new List<GateFaceEnrollmentDto>(templates.Count);

        foreach (var template in templates)
        {
            var descriptors = DeserializeDescriptorSet(template.TemplateData)
                .Where(x => x.Length >= 32)
                .ToArray();
            if (descriptors.Length == 0)
            {
                continue;
            }

            var student = await studentRepository.GetStudentByIdAsync(template.StudentId, cancellationToken: cancellationToken);
            enrollments.Add(new GateFaceEnrollmentDto(
                template.ExternalId,
                student?.FullName ?? "Unknown",
                descriptors));
        }

        return enrollments;
    }

    public async Task<int> GetFaceEnrollmentCountAsync(CancellationToken cancellationToken = default) =>
        (await GetFaceEnrollmentsAsync(cancellationToken)).Count;

    private async Task SaveTemplateAsync(
        int studentId,
        BiometricType type,
        string externalId,
        object templatePayload,
        CancellationToken cancellationToken)
    {
        var json = templatePayload switch
        {
            float[][] set => SerializeDescriptorSet(set),
            IReadOnlyList<float[]> set => SerializeDescriptorSet(set),
            float[] single => SerializeDescriptorSet(new[] { single }),
            string payload => payload,
            _ => throw new InvalidOperationException("Unsupported face template payload.")
        };

        var existing = await localBiometricRepository.GetTemplateAsync(studentId, type, tracking: true, cancellationToken);
        if (existing is null)
        {
            localBiometricRepository.Add(new StudentLocalTemplate
            {
                StudentId = studentId,
                BiometricType = type,
                ExternalId = externalId,
                TemplateData = json
            });
        }
        else
        {
            existing.ExternalId = externalId;
            existing.TemplateData = json;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureBiometricMapAsync(
        int studentId,
        BiometricType type,
        string externalId,
        CancellationToken cancellationToken)
    {
        var device = await biometricDeviceRepository.GetEnabledDeviceForTypeAsync(type, cancellationToken: cancellationToken);
        if (device is null)
        {
            return;
        }

        var map = await studentRepository.GetBiometricMapAsync(studentId, device.Id, tracking: true, cancellationToken);
        if (map is null)
        {
            studentRepository.AddBiometricMap(new StudentBiometricMap
            {
                StudentId = studentId,
                BiometricDeviceId = device.Id,
                BiometricType = type,
                BiometricUserId = externalId
            });
        }
        else
        {
            map.BiometricType = type;
            map.BiometricUserId = externalId;
            map.UpdatedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string BuildExternalId(BiometricType type, int studentId) =>
        type switch
        {
            BiometricType.Face => $"FACE-{studentId}",
            BiometricType.Fingerprint => $"FP-{studentId}",
            _ => $"LOCAL-{studentId}"
        };

    private static void ValidateDescriptor(IReadOnlyList<float> descriptor)
    {
        if (descriptor.Count < 32)
        {
            throw new InvalidOperationException("Face descriptor is invalid.");
        }
    }

    private static string SerializeDescriptorSet(IEnumerable<float[]> descriptors) =>
        JsonSerializer.Serialize(descriptors.Select(static d => d.ToArray()).ToArray(), JsonOptions);

    private static float[] DeserializeDescriptor(string json) =>
        JsonSerializer.Deserialize<float[]>(json, JsonOptions) ?? [];

    private static IReadOnlyList<float[]> DeserializeDescriptorSet(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array
                && document.RootElement.GetArrayLength() > 0
                && document.RootElement[0].ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<float[][]>(json, JsonOptions) ?? [];
            }
        }
        catch (JsonException)
        {
        }

        var single = DeserializeDescriptor(json);
        return single.Length >= 32 ? new[] { single } : [];
    }

    private static string? ReadCredentialId(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("credentialId", out var value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

