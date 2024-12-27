using System;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Timers;

AppConfig GetConfig(string fileName = "config.txt")
{
	if (File.Exists(fileName))
	{
		return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(fileName)) ?? new();
	}
	return new();
}

List<string> GetTargetSites(string fileName = "checklist.txt")
{
	if (File.Exists(fileName))
	{
		return File.ReadAllLines(fileName).ToList();
	}
	return new List<string>();
}

async ValueTask<X509Certificate2?> GetCertificateAsync(string host)
{
	X509Certificate2? cert = null;

	using (var handler = new HttpClientHandler())
	{
		handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
		{
			// Sertifikayı kontrol et
			if (certificate != null)
			{
				cert = certificate;
			}
			return true; // Sertifikayı kabul et
		};

		using (var client = new HttpClient(handler))
		{
			try
			{
				var response = await client.GetAsync(host);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Hata: " + ex.Message);
			}
		}
	}

	return cert;
}

async Task<Dictionary<string,X509Certificate2?>> GetCertificatesAsync(List<string> targets)
{
	Dictionary<string, X509Certificate2?> certs = new();
	await Parallel.ForEachAsync(targets, async (target, token) =>
	{
		var cert = await GetCertificateAsync(target);
		certs.Add(target, cert);
	});

	return certs;
}

void SendExpiryNotification(string senderEmail, string senderPassword, string recipientEmail, string smtpHost, int smtpPort, string server, DateTime expiryDate)
{
	string subject = "SSL Certificate Expire Date is Coming !";
	string body = $"{server} SSL Certificate will be expire on {expiryDate.ToUniversalTime().ToLongDateString()} UTC";

	SmtpClient smtpClient = new SmtpClient(smtpHost, smtpPort)
	{
		Credentials = new NetworkCredential(senderEmail, senderPassword),
		EnableSsl = true
	};

	MailMessage mailMessage = new MailMessage
	{
		From = new MailAddress(senderEmail),
		Subject = subject,
		Body = body,
		IsBodyHtml = false
	};

	mailMessage.To.Add(recipientEmail);

	smtpClient.Send(mailMessage);
}


bool Working = false;

Console.CancelKeyPress += (sender, e) =>
{
	if (!Working)
	{
		Console.WriteLine("Application processing data, please try again later.");
		e.Cancel = true;
	}
};

var config = GetConfig();

var timer = new System.Timers.Timer();
timer.Interval = config.Interval;
timer.Elapsed += async (sender, e) =>
{
	Working = true;
	config = GetConfig(); //Update Config

	var targets = GetTargetSites();
	var targetsWithCerts = await GetCertificatesAsync(targets);

	long maxDate = DateTime.Now.Ticks + TimeSpan.FromDays(7).Ticks;
	foreach(var problematicTarget in targetsWithCerts.Where(x => x.Value != null && x.Value.NotAfter.Ticks < maxDate))
	{
		SendExpiryNotification(config.SenderMail, config.SenderPassword, config.RecipientMail, config.SmtpHost, config.SmtpPort, problematicTarget.Key, problematicTarget.Value!.NotAfter);
	}

	Working = false;
};
timer.Start();

Console.ReadLine();
