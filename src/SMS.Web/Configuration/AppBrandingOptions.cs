namespace SMS.Web.Configuration;

public class AppBrandingOptions
{
    public const string SectionName = "AppBranding";

    public string ApplicationName { get; set; } = "SMS";

    public string ApplicationSubtitle { get; set; } = "School Management System";

    public string CompanyName { get; set; } = "HiveTech";

    public string PoweredByLabel { get; set; } = "Powered by";

    public string CompanyLogoPath { get; set; } = "/branding/company-logo.svg";

    public string? CompanyWebsite { get; set; }

    public string CompanyPhone { get; set; } = "+923465390907";

    public string CompanyPhoneDisplay { get; set; } = "+92 346 5390907";

    public string CompanyWhatsAppUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CompanyPhone))
            {
                return "https://wa.me/";
            }

            var digits = new string(CompanyPhone.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits)
                ? "https://wa.me/"
                : $"https://wa.me/{digits}";
        }
    }
}
