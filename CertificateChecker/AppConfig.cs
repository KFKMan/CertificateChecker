public class AppConfig
{
	public double Interval { get; set; } = TimeSpan.FromDays(1).TotalMilliseconds;
	public string SenderMail { get; set; } = "";
	public string SenderPassword { get; set; } = "";
	public string RecipientMail { get; set; } = "";
	public string SmtpHost { get; set; } = "";
	public int SmtpPort { get; set; } = 587;
}
