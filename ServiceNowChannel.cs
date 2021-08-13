#region Using Directives

using B360.Notifier.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using B360.Common.EntityObjects.Notifier.ExternalNotifications;
using B360.Notifier.Common.Extensions;

#endregion

namespace B360.Notifier.ServiceNowNotification
{
	public class ServiceNowChannel : IChannelNotification<ServiceNowSettings>
	{
    #region Constants

    private const string ServiceNowInstance = "servicenowInstance";
    private const string ServiceNowUrl = "ServiceNowUrl";
    private const string Authentication = "authentication";
    private const string Username = "username";
    private const string Password = "password";
    private const string ShortDescription = "shortDescription";
    private const string Impact = "impact";
    private const string Urgency = "urgency";
    private const string AssignmentGroup = "assignmentGroup";
    private const string Category = "category";
    private const string Subcategory = "subcategory";
    private const string ConfigurationItem = "configurationItem";
    private const string AdditionalComments = "additionalComments";

    #endregion
		public string GetGlobalPropertiesSchema()
		{
			return Helper.GetFileContent("GlobalProperties.json");
		}

		public string GetAlarmPropertiesSchema()
		{
			return Helper.GetFileContent("AlarmProperties.json");
		}

		public bool SendNotification(BizTalkEnvironment environment, Alarm alarm, string globalProperties, Dictionary<MonitorGroupTypeName, MonitorGroupData> notifications)
		{
			try
			{
				var channelSettings = JsonConvert.DeserializeObject<List<CustomNotificationChannel>>(globalProperties);
				var serviceNowSettings = GetSettings(channelSettings);
				if (alarm.AlarmProperties != null)
				{
					var alarmProperties =
						JsonConvert.DeserializeObject<List<CustomNotificationChannelSettings>>(alarm.AlarmProperties);
					if (alarmProperties != null)
					{
						foreach (var property in alarmProperties)
						{
							if (property.Name.CaseInsensitiveEquals(ShortDescription))
							{
								serviceNowSettings.Description = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(Impact))
							{
								serviceNowSettings.Impact = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(Urgency))
							{
								serviceNowSettings.Urgency = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(AssignmentGroup))
							{
								serviceNowSettings.AssignmentGroup = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(Subcategory))
							{
								serviceNowSettings.Subcategory = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(ConfigurationItem))
							{
								serviceNowSettings.ConfigurationItem = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(AdditionalComments))
							{
								serviceNowSettings.AdditionalComments = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(Category))
							{
								serviceNowSettings.Category = property.Value;
							}
						}
					}
				}

				var proxy = channelSettings.GetProxySettings();
				if (channelSettings.IsProxyEnabled())
				{
					proxy.UseDefaultCredentials = proxy.UseDefaultCredentials;
				}
				
				Dictionary<string, string> dictionary = new Dictionary<string, string>
								{
										{ "short_description", serviceNowSettings.Description },
										{ "impact", serviceNowSettings.Impact },
										{ "urgency", serviceNowSettings.Urgency },
										{ "assignment_group", serviceNowSettings.AssignmentGroup },
										{ "category", serviceNowSettings.Category },
										{ "subcategory", serviceNowSettings.Subcategory },
										{ "cmdb_ci", serviceNowSettings.ConfigurationItem },
										{ "comments", serviceNowSettings.AdditionalComments }
								};

				string incidentMessage = string.Empty + string.Format($"\nAlarm Name: {alarm.Name} \n\nAlarm Desc: {alarm.Description} \n" + "\n----------------------------------------------------------------------------------------------------\n" + $"\nEnvironment Name: {environment.Name} \n\nMgmt Sql Instance Name: { Regex.Escape(environment.MgmtSqlInstanceName)} \nMgmt Sql Db Name: {environment.MgmtSqlDbName}\n" + "\n----------------------------------------------------------------------------------------------------\n");
				if (serviceNowSettings.NotifyOnlyOnErrorsAndWarnings)
				{
					foreach (KeyValuePair<MonitorGroupTypeName, MonitorGroupData> notification in notifications)
					{
						foreach (MonitorGroup monitorGroup in notification.Value.monitorGroups)
						{
							Monitors monitors = new Monitors();
              monitors.AddRange(from monitor in monitorGroup.monitors where monitor.monitorStatus != MonitorStatus.Healthy select new Monitor() {issues = monitor.issues, monitorStatus = monitor.monitorStatus, name = monitor.name, serializedConfig = monitor.serializedConfig});
              monitorGroup.monitors = monitors;
						}
					}
				}
				foreach (KeyValuePair<MonitorGroupTypeName, MonitorGroupData> keyValuePair in notifications.OrderBy<KeyValuePair<MonitorGroupTypeName, MonitorGroupData>, MonitorGroupTypeName>(n => n.Key))
				{
					string monitorGroupType = keyValuePair.Key.ToString();
					LoggingHelper.Debug($"Populate the monitor Status{monitorGroupType}");
					foreach (MonitorGroup monitorGroup in keyValuePair.Value.monitorGroups)
					{
						incidentMessage += $"{monitorGroupType} {monitorGroup.name}\n";
						if (monitorGroup.monitors != null)
            {
              incidentMessage = monitorGroup.monitors.Aggregate(incidentMessage, (current, monitor) => current +
                                                                                                       $" - {monitor.name} {monitor.monitorStatus}\n");
            }
					}
					foreach (var monitor in keyValuePair.Value.monitorGroups.Where(monitorGroup => monitorGroup.monitors != null).SelectMany(monitorGroup => monitorGroup.monitors.Where(monitor => monitor.issues != null)))
          {
            incidentMessage += $"\n{monitorGroupType} Issues for {monitor.name}\n";

            foreach (Issue issue in monitor.issues)
            {
              incidentMessage += $" - {issue.description}\n";

              if (issue.monitoringErrorDescriptions != null)
              {
                incidentMessage = issue.monitoringErrorDescriptions.Aggregate(incidentMessage, (current, monitorErrorDescription) => current + $" {monitorErrorDescription.key} ({monitorErrorDescription.count}) -> {monitorErrorDescription.value} \n");
              }

              if (!string.IsNullOrEmpty(issue.optionalDetails))
              {
                incidentMessage += $"{issue.optionalDetails}\n";
              }
            }
          }
					incidentMessage += "\n";
				}

				dictionary.Add("work_notes", incidentMessage);
				LoggingHelper.Debug($"Successfully added to the Incident Object, Message: {incidentMessage}");

				string content = JsonConvert.SerializeObject(dictionary);
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
				HttpClientHandler handler = new HttpClientHandler()
				{
					Proxy = proxy,
					UseProxy = channelSettings.IsProxyEnabled()
				};

				using (HttpClient httpClient = new HttpClient(handler))
				{
					httpClient.BaseAddress = new Uri(serviceNowSettings.ServiceNowUrl);
					httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					HttpResponseMessage result = httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/now/table/incident")
					{
						Headers =
						{
								Authorization = new AuthenticationHeaderValue("Basic",
									Convert.ToBase64String(
										Encoding.ASCII.GetBytes($"{serviceNowSettings.UserName}:{serviceNowSettings.Password}")))
						},
						Content = new StringContent(content, Encoding.UTF8, "application/json")
					}).Result;
					if (result.IsSuccessStatusCode)
					{
						LoggingHelper.Info("ServiceNow Incident Creation was Successful");
					}
					else
					{
						LoggingHelper.Error($"ServiceNow Incident Creation was Unsuccessful. \n Response: {result}");
					}

					return result.IsSuccessStatusCode;
				}
			}
			catch (Exception ex)
			{
				LoggingHelper.Fatal(ex.ToString());
				LoggingHelper.Fatal(ex.StackTrace);
				LoggingHelper.Error(ex.Message);
				return false;
			}
		}

		public ServiceNowSettings GetSettings(List<CustomNotificationChannel> channelSettings)
		{
			var result = new ServiceNowSettings();
            result.ApplyCommonSettings(channelSettings);
			var instanceSettings = channelSettings.Find(x => x.Name.CaseInsensitiveEquals(ServiceNowInstance));
			foreach (var instance in instanceSettings.Data.Where(instance =>
				instance.Name.CaseInsensitiveEquals(ServiceNowUrl)))
			{
				result.ServiceNowUrl = instance.Value;
			}
			var authenticationSettings = channelSettings.Find(x => x.Name.CaseInsensitiveEquals(Authentication));
			foreach (var authSetting in authenticationSettings.Data)
			{
				if (authSetting.Name.CaseInsensitiveEquals(Username))
				{
					result.UserName = authSetting.Value;
				}
				if (authSetting.Name.CaseInsensitiveEquals(Password))
				{
					result.Password = authSetting.Value;
				}
			}
			return result;
		}
	}
}
