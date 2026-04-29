namespace AFWebChat.Models;

public class AppBrandingSettings
{
    public string LogoUrl { get; set; } = "";
    public string Title { get; set; } = "AF-WebChat";
    public string Subtitle { get; set; } = "Agent Framework Web Chat";
    public string Theme { get; set; } = "dark"; // "dark" or "light"
    public string PrimaryColor { get; set; } = "#ffffff";
    public string SecondaryColor { get; set; } = "#58a6ff";
    public string BackgroundColor { get; set; } = "#0d1117";
    public string NavbarColor { get; set; } = "#1a1a2e";
    public string TextColor { get; set; } = "#ffffff";
    public string AccentColor { get; set; } = "#C9A227";
    public string SuccessColor { get; set; } = "#238636";
    public string ErrorColor { get; set; } = "#f85149";
    public string LogoIcon { get; set; } = "bi-robot";
    public string WelcomeMessage { get; set; } = "Interact with AI agents powered by Microsoft Agent Framework, design orchestration workflows, and visualize execution in real-time";
    public string ChatWelcomeTitle { get; set; } = "Welcome to AF-WebChat";
    public string ChatWelcomeSubtitle { get; set; } = "Select an agent and start chatting.";
    public string FooterText { get; set; } = "";

    // Animated background shapes
    public string Shape1ColorFrom { get; set; } = "#0078d4";
    public string Shape1ColorTo { get; set; } = "#00bcf2";
    public string Shape2ColorFrom { get; set; } = "#8764b8";
    public string Shape2ColorTo { get; set; } = "#e81123";
    public string Shape3ColorFrom { get; set; } = "#107c10";
    public string Shape3ColorTo { get; set; } = "#ff8c00";
    public int ShapeAnimationSeconds { get; set; } = 10;
}
