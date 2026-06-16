using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using MimeKit.Text;
using Pronetsys.Shared.Entities;

namespace Pronetsys.Server.Services;

public interface IEmailSenderEx
{
    Task<bool> SendEmailAsync(string email, string replyTo, string subject, string htmlMessage, string? organizationID = null);

    Task<bool> SendEmailAsync(string email, string subject, string htmlMessage, string? organizationID = null);
}

public class EmailSender : IEmailSender
{
    public EmailSender(IEmailSenderEx emailSenderEx)
    {
        EmailSenderEx = emailSenderEx;
    }

    private IEmailSenderEx EmailSenderEx { get; }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        return EmailSenderEx.SendEmailAsync(email, subject, htmlMessage, string.Empty);
    }
}

public class EmailSenderEx : IEmailSenderEx, IEmailSender<PronetsysUser>
{
    private readonly IDataService _dataService;
    private readonly ILogger<EmailSenderEx> _logger;

    public EmailSenderEx(
        IDataService dataService,
        ILogger<EmailSenderEx> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(PronetsysUser user, string email, string confirmationLink)
    {
        var baseUrl = string.Empty;
        try
        {
            baseUrl = new Uri(System.Net.WebUtility.HtmlDecode(confirmationLink)).GetLeftPart(UriPartial.Authority);
        }
        catch { }

        var html = BuildBrandedHtml(
            baseUrl,
            "Confirma tu cuenta",
            "¡Te damos la bienvenida a <strong>Pronetsys Asistencia Remota</strong>! Para activar tu cuenta, " +
            "confirma tu correo electrónico haciendo clic en el botón.",
            "Confirmar cuenta",
            confirmationLink,
            "Si no creaste esta cuenta, puedes ignorar este correo.");

        await SendEmailAsync(email, "Confirma tu cuenta — Pronetsys", html, user.OrganizationID);
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string replyTo,
        string subject,
        string htmlMessage,
        string? organizationID = null)
    {
        try
        {
            var settings = await _dataService.GetSettings();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SmtpDisplayName, settings.SmtpEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            message.Subject = subject;
            message.Body = new TextPart(TextFormat.Html)
            {
                Text = htmlMessage
            };

            using var client = new SmtpClient();

            if (!string.IsNullOrWhiteSpace(settings.SmtpLocalDomain))
            {
                client.LocalDomain = settings.SmtpLocalDomain;
            }

            client.CheckCertificateRevocation = settings.SmtpCheckCertificateRevocation;

            await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort);

            if (!string.IsNullOrWhiteSpace(settings.SmtpUserName) &&
                !string.IsNullOrWhiteSpace(settings.SmtpPassword))
            {
                await client.AuthenticateAsync(settings.SmtpUserName, settings.SmtpPassword);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email successfully sent to {toEmail}.  Subject: \"{subject}\".", toEmail, subject);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while sending email.");
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(string email, string subject, string htmlMessage, string? organizationID = null)
    {
        var settings = await _dataService.GetSettings();
        return await SendEmailAsync(email, settings.SmtpEmail, subject, htmlMessage, organizationID);
    }

    public async Task SendPasswordResetCodeAsync(PronetsysUser user, string email, string resetCode)
    {
        await SendEmailAsync(
            email,
            "Código de restablecimiento — Pronetsys",
            "Se solicitó restablecer la contraseña de tu cuenta de Pronetsys Asistencia Remota.<br/><br/>" +
            $"Tu código de restablecimiento es: <strong>{resetCode}</strong>",
            user.OrganizationID);
    }

    public async Task SendPasswordResetLinkAsync(PronetsysUser user, string email, string resetLink)
    {
        var baseUrl = string.Empty;
        try
        {
            baseUrl = new Uri(System.Net.WebUtility.HtmlDecode(resetLink)).GetLeftPart(UriPartial.Authority);
        }
        catch
        {
            // resetLink wasn't parseable as an absolute URL; the email still works without the logo.
        }

        var html = BuildBrandedHtml(
            baseUrl,
            "Restablecimiento de contraseña",
            "Recibimos una solicitud para restablecer la contraseña de tu cuenta de <strong>Pronetsys Asistencia Remota</strong>. " +
            "Haz clic en el botón para crear una contraseña nueva.",
            "Restablecer contraseña",
            resetLink,
            "Si no solicitaste este cambio, puedes ignorar este correo: tu contraseña seguirá igual. " +
            "Por seguridad, este enlace es de un solo uso y caduca pronto.");

        await SendEmailAsync(email, "Restablecimiento de contraseña — Pronetsys", html, user.OrganizationID);
    }

    // Corporate Pronetsys HTML email wrapper (table-based + inline styles for email-client compatibility).
    private static string BuildBrandedHtml(
        string baseUrl,
        string heading,
        string introHtml,
        string buttonText,
        string buttonUrl,
        string footerNote)
    {
        var logoUrl = $"{baseUrl}/images/pronetsys-logo-white.png";
        var year = DateTime.Now.Year;
        const string font = "Segoe UI,Arial,Helvetica,sans-serif";

        return $@"<!DOCTYPE html>
<html lang='es'>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>
<body style='margin:0;padding:0;background-color:#eef1f5;'>
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background-color:#eef1f5;padding:24px 0;font-family:{font};'>
<tr><td align='center'>
<table role='presentation' width='600' cellpadding='0' cellspacing='0' style='width:600px;max-width:600px;background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e3e8ee;'>
<tr><td align='center' style='background-color:#0065cb;padding:22px;'>
<img src='{logoUrl}' alt='Pronetsys Asistencia Remota' width='210' style='display:block;border:0;outline:none;max-width:210px;height:auto;' />
</td></tr>
<tr><td style='padding:34px 36px 4px 36px;'>
<h1 style='margin:0 0 16px 0;font-size:22px;color:#0d1821;font-weight:600;font-family:{font};'>{heading}</h1>
<p style='margin:0 0 26px 0;font-size:15px;line-height:1.6;color:#444444;font-family:{font};'>{introHtml}</p>
</td></tr>
<tr><td align='center' style='padding:0 36px 28px 36px;'>
<table role='presentation' cellpadding='0' cellspacing='0'><tr><td align='center' bgcolor='#0065cb' style='border-radius:8px;'>
<a href='{buttonUrl}' target='_blank' style='display:inline-block;padding:14px 42px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;background-color:#0065cb;font-family:{font};'>{buttonText}</a>
</td></tr></table>
</td></tr>
<tr><td style='padding:0 36px 22px 36px;color:#8a9099;font-size:12px;line-height:1.5;font-family:{font};'>
<p style='margin:0 0 6px 0;'>Si el botón no funciona, copia y pega este enlace en tu navegador:</p>
<p style='margin:0;word-break:break-all;'><a href='{buttonUrl}' style='color:#0065cb;'>{buttonUrl}</a></p>
</td></tr>
<tr><td style='padding:18px 36px 26px 36px;color:#8a9099;font-size:12px;line-height:1.6;border-top:1px solid #eeeeee;font-family:{font};'>{footerNote}</td></tr>
<tr><td align='center' style='background-color:#0d1821;padding:18px;color:#9fb0c0;font-size:12px;font-family:{font};'>&copy; {year} Pronetsys Asistencia Remota</td></tr>
</table>
</td></tr>
</table>
</body>
</html>";
    }
}

public class EmailSenderFake(ILogger<EmailSenderFake> _logger) : IEmailSenderEx
{
    public Task<bool> SendEmailAsync(string email, string replyTo, string subject, string htmlMessage, string? organizationID = null)
    {
        _logger.LogInformation(
            "Fake EmailSender registered in dev mode. " +
            "Email would have been sent to {email}." +
            "\n\nSubject: {EmailSubject}. " +
            "\n\nMessage: {EmailMessage}",
            email,
            subject,
            htmlMessage);
        return Task.FromResult(true);
    }

    public Task<bool> SendEmailAsync(string email, string subject, string htmlMessage, string? organizationID = null)
    {
        return SendEmailAsync(email, "", subject, htmlMessage, organizationID);
    }
}