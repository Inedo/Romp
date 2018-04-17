using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Web.Server;

namespace Inedo.Romp.Extensions.Operations
{
    [Serializable]
    [ScriptAlias("Ensure-UrlReservation")]
    [ScriptNamespace("InedoInternal", PreferUnqualified = false)]
    public sealed class EnsureUrlReservationOperation : RemoteEnsureOperation<UrlReservationConfiguration>
    {
        protected override Task RemoteConfigureAsync(IRemoteOperationExecutionContext context)
        {
            this.LogDebug($"Finding current reservations for {this.Template.Url}...");
            var current = HttpApi.GetAllReservations()
                .FirstOrDefault(r => string.Equals(r.Prefix, this.Template.Url, StringComparison.OrdinalIgnoreCase));

            if (current != null)
                this.LogDebug("Existing reservation found: " + current);
            else
                this.LogDebug("URL is not currently reserved.");

            if (this.Template.Exists)
            {
                if (string.IsNullOrWhiteSpace(this.Template.UserAccount))
                {
                    this.LogError("User account must be specified when creating a new reservation.");
                    return Complete();
                }

                UrlReservationAccount account;
                try
                {
                    account = new UrlReservationAccount(this.Template.UserAccount);
                }
                catch (Exception ex)
                {
                    this.LogError("Invalid user account: " + ex.Message);
                    return Complete();
                }

                UrlReservation reservation;
                try
                {
                    reservation = new UrlReservation(this.Template.Url, account);
                }
                catch (Exception ex)
                {
                    this.LogError("Invalid URL: " + ex.Message);
                    return Complete();
                }

                if (current != null && current.Accounts.Any(a => a.Sid == account.Sid))
                {
                    this.LogInformation("URL is already reserved for the specified user account.");
                }
                else
                {
                    this.LogInformation($"Reserving URL for {this.Template.UserAccount}...");
                    HttpApi.ReserveUrl(reservation);
                    this.LogInformation("Reservation added.");
                }
            }
            else
            {
                if (current != null)
                {
                    this.LogInformation($"Deleting reservation for {this.Template.Url}...");
                    try
                    {
                        HttpApi.DeleteReservation(current.Prefix);
                    }
                    catch (Exception ex)
                    {
                        this.LogError("Error deleting reservation: " + ex.Message);
                        return Complete();
                    }

                    this.LogInformation("Reservation deleted.");
                }
                else
                {
                    this.LogInformation("URL is already not reserved.");
                }
            }

            return Complete();
        }
        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            if (string.Equals(config[nameof(UrlReservationConfiguration.Exists)], "false", StringComparison.OrdinalIgnoreCase))
            {
                return new ExtendedRichDescription(
                    new RichDescription(
                        "Ensure that there are no reservations for ",
                        new Hilite(config[nameof(UrlReservationConfiguration.Url)])
                    )
                );
            }
            else
            {
                return new ExtendedRichDescription(
                    new RichDescription(
                        "Ensure that ",
                        new Hilite(config[nameof(UrlReservationConfiguration.Url)])
                    ),
                    new RichDescription(
                        "is registered to ",
                        new Hilite((string)config[nameof(UrlReservationConfiguration.UserAccount)] ?? "<INVALID>")
                    )
                );
            }
        }
    }

    [Serializable]
    public sealed class UrlReservationConfiguration : PersistedConfiguration, IExistential
    {
        [Required]
        [ConfigurationKey]
        [ScriptAlias("Url")]
        public string Url { get; set; }
        [ScriptAlias("User")]
        public string UserAccount { get; set; }
        [DefaultValue(true)]
        [ScriptAlias("Exists")]
        public bool Exists { get; set; } = true;
    }
}
