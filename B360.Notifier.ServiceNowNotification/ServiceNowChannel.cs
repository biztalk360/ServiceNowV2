using B360.Common.EntityObjects.Notifier.ExternalNotifications;
using B360.Notifier.Common;
using B360.Notifier.Common.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
		private const string Incident = "incident";
		private const string Event = "event";
		private const string SendEventPerIssue = "sendEventPerIssue";
		private const string SendEventAs = "sendEventAs";
		private const string Severity = "severity";
		private const string SeveritySettings = "severitySettings";
		private const string Application = "Application";
		private const string BizTalk = "BizTalk";
		private const string SqlServerInstance = "SqlServerInstance";
		private const string SqlServer = "SqlServer";
		private const string Servers = "Servers";

		int severity = (int)EventSeverity.ok;

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
				bool isStatusCode = false;
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
							if (property.Name.CaseInsensitiveEquals(Incident))
							{
								serviceNowSettings.Incident = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(Event))
							{
								serviceNowSettings.Event = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(SendEventPerIssue))
							{
								serviceNowSettings.SendEventPerIssue = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(SendEventAs))
							{
								serviceNowSettings.SendEventAs = property.Value;
							}
							if (property.Name.CaseInsensitiveEquals(ShortDescription))
							{
								serviceNowSettings.Description = property.Value;
							}
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
							if (property.Name.CaseInsensitiveEquals(SeveritySettings))
							{
								serviceNowSettings.SeveritySettings = property.Value;
							}
							if (serviceNowSettings.SeveritySettings == "true")
							{
								if (property.Name.CaseInsensitiveEquals(Severity))
								{
									serviceNowSettings.Severity = GetServiceNowEventSeverity(property.Value, serviceNowSettings);
								}
								severity = (int)serviceNowSettings.Severity;
							}
						}
					}
				}
				var proxy = channelSettings.GetProxySettings();

				if (serviceNowSettings.Incident == "true")
				{
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
								monitors.AddRange(from monitor in monitorGroup.monitors where monitor.monitorStatus != MonitorStatus.Healthy select new Monitor() { issues = monitor.issues, monitorStatus = monitor.monitorStatus, name = monitor.name, serializedConfig = monitor.serializedConfig });
								monitorGroup.monitors = monitors;
							}
						}
					}
					foreach (KeyValuePair<MonitorGroupTypeName, MonitorGroupData> keyValuePair in notifications.OrderBy<KeyValuePair<MonitorGroupTypeName, MonitorGroupData>, MonitorGroupTypeName>(n => n.Key))
					{
						string monitorGroupType = keyValuePair.Key.ToString();
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
						isStatusCode = result.IsSuccessStatusCode;
					}
				}
				if (serviceNowSettings.Event == "true")
				{
					string eventDescription = string.Empty;
					string resource = string.Empty;
					string type = string.Empty;
					string metric_name = string.Empty;

					if (serviceNowSettings.NotifyOnlyOnErrorsAndWarnings)
					{
						foreach (KeyValuePair<MonitorGroupTypeName, MonitorGroupData> notification in notifications)
						{
							foreach (MonitorGroup monitorGroup in notification.Value.monitorGroups)
							{
								Monitors monitors = new Monitors();
								foreach (Monitor monitor in monitorGroup.monitors)
								{
									if (monitor.monitorStatus != MonitorStatus.Healthy)
									{
										monitors.Add(new Monitor()
										{
											issues = monitor.issues,
											monitorStatus = monitor.monitorStatus,
											name = monitor.name,
											serializedConfig = monitor.serializedConfig
										});
									}
								}
								monitorGroup.monitors = monitors;
							}
						}
					}

					if (serviceNowSettings.SendEventAs != SendEventPerIssue)
					{
						foreach (KeyValuePair<MonitorGroupTypeName, MonitorGroupData> keyValuePair in notifications.OrderBy<KeyValuePair<MonitorGroupTypeName, MonitorGroupData>, MonitorGroupTypeName>(n => n.Key))
						{
							string monitorGroupType = keyValuePair.Key.ToString();

							type += monitorGroupType;

							bool isStatusCritical = keyValuePair.Value.monitorGroups.Any((monitorGroup) => monitorGroup.monitorStatus == MonitorStatus.Critical);
							bool isStatusHealthy = keyValuePair.Value.monitorGroups.All((monitorGroup) => monitorGroup.monitorStatus == MonitorStatus.Healthy);

							if (serviceNowSettings.SeveritySettings != "true")
							{
								if (severity != (int)EventSeverity.critical)
								{
									severity = isStatusCritical == true ? (int)EventSeverity.critical : (int)EventSeverity.ok;
								}
							}

							foreach (MonitorGroup monitorGroup in keyValuePair.Value.monitorGroups)
							{
								var monitorStatus = monitorGroup.monitorStatus;

								if (monitorGroupType == Application)
								{
									resource += monitorGroup.name;
								}
								if (monitorGroup.monitors != null)
								{
									if (serviceNowSettings.NotifyOnlyOnErrorsAndWarnings)
									{
										if (monitorStatus != MonitorStatus.Healthy)
										{
											if (monitorGroupType == Application || monitorGroupType == BizTalk || monitorGroupType == SqlServer || monitorGroupType == SqlServerInstance || monitorGroupType == Servers)
											{
												eventDescription += $"\n{monitorGroupType} {monitorGroup.name}\n";
											}
											else
											{
												eventDescription += $"\n{monitorGroupType}\n";
											}
										}
									}
									else
									{
										if (monitorGroupType == Application || monitorGroupType == BizTalk || monitorGroupType == SqlServer || monitorGroupType == SqlServerInstance || monitorGroupType == Servers)
										{
											eventDescription += $"\n{monitorGroupType} {monitorGroup.name}\n";
										}
										else
										{
											eventDescription += $"\n{monitorGroupType}\n";
										}
									}

									eventDescription = monitorGroup.monitors.Aggregate(eventDescription, (current, monitor) => current +
																																																					 $"\n - {monitor.name} {monitor.monitorStatus}\n");
								}
								resource += ",";
							}
							foreach (var monitor in keyValuePair.Value.monitorGroups.Where(monitorGroup => monitorGroup.monitors != null).SelectMany(monitorGroup => monitorGroup.monitors.Where(monitor => monitor.issues != null)))
							{
								if (monitor.issues != null)
								{
									if (monitor.monitorStatus != MonitorStatus.Healthy)
									{
										eventDescription += $"\n{monitorGroupType} Issues for {monitor.name}\n";
										foreach (Issue issue in monitor.issues)
										{
											eventDescription += $"\n - {issue.description}\n";
											if (issue.monitoringErrorDescriptions != null)
											{
												eventDescription = issue.monitoringErrorDescriptions.Aggregate(eventDescription, (current, monitorErrorDescription) => current + $" {monitorErrorDescription.key} ({monitorErrorDescription.count}) -> {monitorErrorDescription.value} \n");
											}
											if (!string.IsNullOrEmpty(issue.optionalDetails))
											{
												eventDescription += $"{issue.optionalDetails}\n";
											}
										}
									}
								}
							}
							eventDescription += "\n";
							type += ",";
						}
						isStatusCode = SendServiceNowEvent(alarm, resource, environment.Name, type, serviceNowSettings, eventDescription, proxy, channelSettings, isStatusCode, severity);
					}
					else
					{
						foreach (KeyValuePair<MonitorGroupTypeName, MonitorGroupData> keyValuePair in notifications.OrderBy<KeyValuePair<MonitorGroupTypeName, MonitorGroupData>, MonitorGroupTypeName>(n => n.Key))
						{
							string monitorGroupType = keyValuePair.Key.ToString();
							type = monitorGroupType;

							foreach (MonitorGroup monitorGroup in keyValuePair.Value.monitorGroups)
							{
								var monitorStatus = monitorGroup.monitorStatus;

								if (monitorGroupType == Application)
								{
									resource += monitorGroup.name;
								}
								if (monitorGroup.monitors != null)
								{
									if (serviceNowSettings.NotifyOnlyOnErrorsAndWarnings)
									{
										if (monitorStatus != MonitorStatus.Healthy)
										{
											if (monitorGroupType == Application || monitorGroupType == BizTalk || monitorGroupType == SqlServer || monitorGroupType == SqlServerInstance || monitorGroupType == Servers)
											{
												eventDescription = $"\n{monitorGroupType} {monitorGroup.name}\n";
											}
											else
											{
												eventDescription = $"\n{monitorGroupType}\n";
											}
										}
									}
									else
									{
										if (monitorGroupType == Application || monitorGroupType == BizTalk || monitorGroupType == SqlServer || monitorGroupType == SqlServerInstance || monitorGroupType == Servers)
										{
											eventDescription = $"\n{monitorGroupType} {monitorGroup.name}\n";
										}
										else
										{
											eventDescription = $"\n{monitorGroupType}\n";
										}
									}
								}
							}
							foreach (var monitor in keyValuePair.Value.monitorGroups.Where(monitorGroup => monitorGroup.monitors != null).SelectMany(monitorGroup => monitorGroup.monitors.Where(monitor => monitor.issues != null)))
							{
								eventDescription += $"\n - {monitor.name} {monitor.monitorStatus}\n";
								if (serviceNowSettings.SeveritySettings != "true")
								{
									switch (monitor.monitorStatus)
									{
										case MonitorStatus.Critical:
											severity = (int)EventSeverity.critical;
											break;
										case MonitorStatus.Healthy:
											severity = (int)EventSeverity.ok;
											break;
										case MonitorStatus.Warning:
											severity = (int)EventSeverity.warning;
											break;
										case MonitorStatus.MonitorError:
											severity = (int)EventSeverity.critical;
											break;
										case MonitorStatus.NotConfigured:
											severity = (int)EventSeverity.minor;
											break;
										case MonitorStatus.Unknown:
											severity = (int)EventSeverity.minor;
											break;
									}
								}
								if (monitor.monitorStatus == MonitorStatus.Healthy && !serviceNowSettings.NotifyOnlyOnErrorsAndWarnings)
								{
									isStatusCode = SendServiceNowEvent(alarm, resource, environment.Name, type, serviceNowSettings, eventDescription, proxy, channelSettings, isStatusCode, severity);
								}
								foreach (Issue issue in monitor.issues)
								{

									if (monitor.monitorStatus != MonitorStatus.Healthy)
									{
										eventDescription += $"\n{monitorGroupType} Issues for {monitor.name}\n";
										eventDescription += $"\n - {issue.description}\n";
										if (issue.monitoringErrorDescriptions != null)
										{
											eventDescription = issue.monitoringErrorDescriptions.Aggregate(eventDescription, (current, monitorErrorDescription) => current + $" {monitorErrorDescription.key} ({monitorErrorDescription.count}) -> {monitorErrorDescription.value} \n");
										}
										if (!string.IsNullOrEmpty(issue.optionalDetails))
										{
											eventDescription += $"{issue.optionalDetails}\n";
										}
									}
									isStatusCode = SendServiceNowEvent(alarm, resource, environment.Name, type, serviceNowSettings, eventDescription, proxy, channelSettings, isStatusCode, severity);
								}
							}
						}
					}
				}
				return isStatusCode;
			}
			catch (Exception ex)
			{
				LoggingHelper.Error(ex.Message);
				LoggingHelper.Error(ex.StackTrace);
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

		public bool SendAnalyticsReport(BizTalkEnvironment environment, ReportingScheduler scheduler, string globalProperties, string getAnalyticsReportInStreamApi)
		{
			try
			{
				#region Message Template  

				string incidentMessage = string.Empty;
				if (scheduler.ReportType == (int)ReportType.dashboard)
				{
					incidentMessage = $"\n Report Name: {scheduler.Name} \n Report Dashboard: {scheduler.ResourceName} \n";
					incidentMessage += "\n----------------------------------------------------------------------------------------------------\n";
					incidentMessage += $"\n Environment Name: {Regex.Escape(environment.Name)} \n Mgmt Sql Instance Name: { Regex.Escape(environment.MgmtSqlInstanceName)} \n Mgmt Sql Db Name: {environment.MgmtSqlDbName}\n" + "\n----------------------------------------------------------------------------------------------------\n";


					dynamic serializedConfiguredWidgets = JsonConvert.DeserializeObject(scheduler.ConfiguredWidgets);

					if (serializedConfiguredWidgets.Count > 0)
					{
						incidentMessage += "\n Configured Widgets \n";
						foreach (var configuredObject in serializedConfiguredWidgets)
						{
							incidentMessage += $"\n {configuredObject.title} \r";
						}
						incidentMessage += "\n\n----------------------------------------------------------------------------------------------------\n";
					}
				}
				else
				{
					incidentMessage = $"\n Report Name: {scheduler.Name} \n";
					incidentMessage += "\n----------------------------------------------------------------------------------------------------\n";
					incidentMessage += $"\n Environment Name: {Regex.Escape(environment.Name)} \n Mgmt Sql Instance Name: { Regex.Escape(environment.MgmtSqlInstanceName)} \n Mgmt Sql Db Name: {environment.MgmtSqlDbName}\n" + "\n----------------------------------------------------------------------------------------------------\n";
					incidentMessage += $"\n Environment Name: {Regex.Escape(environment.Name)} \n Mgmt Sql Instance Name: { Regex.Escape(environment.MgmtSqlInstanceName)} \n Mgmt Sql Db Name: {environment.MgmtSqlDbName}\n" + "\n----------------------------------------------------------------------------------------------------\n";
					incidentMessage += $"\n Secure SQL Query: {scheduler.CustomSQLQuery.Name}  \n";
					incidentMessage += $"\n SQL Instance: {scheduler.CustomSQLQuery.SqlInstance}  \n";
					incidentMessage += $"\n Database: {scheduler.CustomSQLQuery.Database} \n";
					incidentMessage += $"\n No of Records: {scheduler.CustomSQLQuery.SQLQueryRecordCount} \n";
					incidentMessage += "\n----------------------------------------------------------------------------------------------------\n";
				}

				return SendMessageToNotificationChannel(globalProperties, scheduler.ScheduleProperties, incidentMessage, scheduler.FileStream, scheduler.AttachmentFileName);

				#endregion
			}
			catch (Exception ex)
			{
				LoggingHelper.Fatal(ex.ToString());
				LoggingHelper.Fatal(ex.StackTrace);
				LoggingHelper.Error(ex.Message);
				return false;
			}
		}

		public bool SendAutomatedTaskSummary(BizTalkEnvironment environment, AutomatedTaskInstanceDetail automatedTaskInstanceDetail, string globalProperties)
		{
			try
			{

				#region Template
				string incidentMessage = string.Empty;
				incidentMessage += ($"\n Automated Task: {automatedTaskInstanceDetail.taskName}");
				incidentMessage += !string.IsNullOrEmpty(automatedTaskInstanceDetail.description) ? ($"\n {automatedTaskInstanceDetail.description} \n") : ($"\n");
				incidentMessage += "\n----------------------------------------------------------------------------------------------------\n";
				incidentMessage += ($"\n Environment Name: {environment.Name} \n Mgmt SQL Instance Name: {Regex.Escape(environment.MgmtSqlInstanceName)} \n Mgmt SQL Db Name: {environment.MgmtSqlDbName} \n");
				incidentMessage += "\n----------------------------------------------------------------------------------------------------\n";
				incidentMessage += ($"\n Automated Task Execution Summary \n");
				List<GroupedResources> deserializedGroupedResources = JsonConvert.DeserializeObject<List<GroupedResources>>(automatedTaskInstanceDetail.actionExecutionDetails[0].groupedResourcesList);
				if (automatedTaskInstanceDetail.configurationType != ConfigurationType.CustomWorkflow)
				{
					switch (automatedTaskInstanceDetail.configurationType)
					{
						case ConfigurationType.Application:
						case ConfigurationType.PowerShellScript:
							incidentMessage += ($"\n Resource Type: {automatedTaskInstanceDetail.configurationType} " +
							$"\n Resources Name: {automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().resourcesName} ");
							break;
						case ConfigurationType.LogicApps:
							incidentMessage += ($"\n Resource Type: {automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().resourceType} "
								+ $"\n Resources Detail: ");
							foreach (GroupedResources group in deserializedGroupedResources)
							{
								if (group.groupCategory != AutomatedTaskConstants.ResubmittedRuns)
								{
									incidentMessage += $"\n - {group.groupName} \n";
									incidentMessage += $" {string.Join(", ", group.groupMembers)} ";
									incidentMessage += $"\n";
								}
							}
							break;
						default:
							incidentMessage += ($"\n Resource Type: {automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().resourceType} "
								+ $"\n Resources Detail: ");
							foreach (GroupedResources group in deserializedGroupedResources)
							{
								incidentMessage += $"\n - {group.groupName} \n";
								incidentMessage += $" {string.Join(", ", group.groupMembers)} ";
								incidentMessage += $"\n";
							}
							break;
					}

					incidentMessage += $"\n Action: {automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().actionPerformed} ";
					if (automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().actionPerformed == AutomatedTaskConstants.ResubmitAction)
					{
						incidentMessage += $"\n Resubmitted Runs: ";
						foreach (GroupedResources group in deserializedGroupedResources)
						{
							if (group.groupCategory == AutomatedTaskConstants.ResubmittedRuns)
							{
								incidentMessage += $"\n - {group.groupName} \n";
								incidentMessage += $" {string.Join(", ", group.groupMembers)} ";
								incidentMessage += $"\n";
							}
						}
					}
					incidentMessage +=
						$"\n Status: {automatedTaskInstanceDetail.taskStatus} " +
						$"\n Execution Type: {automatedTaskInstanceDetail.executionType} " +
						$"\n Started At: {automatedTaskInstanceDetail.startedAt} " +
						$"\n Completed At: {automatedTaskInstanceDetail.completedAt}";

					if (automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().issues?.Count > 0)
					{
						incidentMessage += ($"\n Failure Reasons: \n");
						foreach (IssueDetail issue in automatedTaskInstanceDetail.actionExecutionDetails.FirstOrDefault().issues)
						{
							if (automatedTaskInstanceDetail.configurationType == ConfigurationType.Application)
								incidentMessage += ($"\n - {issue.resourceType}: {issue.failureReason}");
							else
								incidentMessage += ($"\n - {issue.failureReason}");
						}
					}
				}
				else if (automatedTaskInstanceDetail.configurationType == ConfigurationType.CustomWorkflow)
				{
					incidentMessage += ($"\n Resource Type: {automatedTaskInstanceDetail.configurationType}" +
						$"\n Overall Status: {automatedTaskInstanceDetail.taskStatus} " +
						$"\n Execution Type: {automatedTaskInstanceDetail.executionType} " +
						$"\n Started At: {automatedTaskInstanceDetail.startedAt} " +
						$"\n Completed At: {automatedTaskInstanceDetail.completedAt}");

					foreach (ActionExecutionDetails actionExecutionDetail in automatedTaskInstanceDetail.actionExecutionDetails)
					{
						deserializedGroupedResources = JsonConvert.DeserializeObject<List<GroupedResources>>(actionExecutionDetail.groupedResourcesList);
						incidentMessage += "\n----------------------------------------------------------------------------------------------------\n";
						incidentMessage += ($"\n Step {actionExecutionDetail.configuredOrder} - {actionExecutionDetail.actionPerformed}\n" +
						$"\n Status: {actionExecutionDetail.actionStatus} ");
						incidentMessage += ($"\n Resource Type: {actionExecutionDetail.resourceType} "
								+ $"\n Resources Detail: \n");
						foreach (GroupedResources group in deserializedGroupedResources)
						{
							incidentMessage += $"\n - {group.groupName} \n";
							incidentMessage += $" {string.Join(", ", group.groupMembers)} ";
							incidentMessage += $"\n";
						}

						if (actionExecutionDetail.issues?.Count > 0)
						{
							incidentMessage += ($"\n Failure Reasons: \n");
							foreach (IssueDetail issue in actionExecutionDetail.issues)
							{
								incidentMessage += ($"\n - {issue.failureReason}");
							}
						}
					}
				}
				#endregion

				return SendMessageToNotificationChannel(globalProperties, automatedTaskInstanceDetail.taskNotificationProperties, incidentMessage);
			}
			catch (Exception ex)
			{
				LoggingHelper.Info($"Service Now notification failed for Automated Task {automatedTaskInstanceDetail.taskName}. Error " + ex.Message);
				return false;
			}

		}
		public bool SendMessageToNotificationChannel(string globalProperties, string additionalNotificationProperties, string incidentMessage, MemoryStream fileStream = null, string attachmentFileName = null)
		{
			try
			{
				var channelSettings = JsonConvert.DeserializeObject<List<CustomNotificationChannel>>(globalProperties);
				ServiceNowSettings serviceNowSettings = GetSettings(channelSettings);
				if (!string.IsNullOrEmpty(additionalNotificationProperties))
				{
					var overwrittenProperties =
							JsonConvert.DeserializeObject<List<CustomNotificationChannelSettings>>(additionalNotificationProperties);
					if (overwrittenProperties != null)
					{
						GetServiceNowProperties(overwrittenProperties, serviceNowSettings);
					}
				}
				var proxy = channelSettings.GetProxySettings();

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
				dictionary.Add("work_notes", incidentMessage);
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
					var response = result.Content.ReadAsStringAsync();
					if (result.IsSuccessStatusCode)
					{
						LoggingHelper.Info("ServiceNow Incident Creation was Successful");
						if (fileStream != null && attachmentFileName != null)
						{
							// Get the Sys_Id from the successfully created incident for uploading the attachment
							dynamic apiResult = JsonConvert.DeserializeObject(response.Result);
							string sysId = apiResult.result.sys_id;
							var pushAttachmentResult = PushAttachmentToIncidentAsync(serviceNowSettings, fileStream, sysId, attachmentFileName, proxy, channelSettings);
							if (pushAttachmentResult)
							{
								LoggingHelper.Info($"Attachment to the ServiceNow incident {sysId} was Successful");
							}
							else
							{
								LoggingHelper.Error($"Attachment to the ServiceNow incident {sysId} was unsuccessful");
							}
						}
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
				LoggingHelper.Error(ex.Message);
				LoggingHelper.Error(ex.StackTrace);
				return false;
			}
		}
		public bool PushAttachmentToIncidentAsync(ServiceNowSettings serviceNowSettings, MemoryStream pdfFileStream, string sysId, string attachmentFileName, WebProxy proxy, List<CustomNotificationChannel> channelSettings)
		{
			try
			{
				string attachFileUrl = $"{serviceNowSettings.ServiceNowUrl}/api/now/attachment/upload";
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
				HttpClientHandler handler = new HttpClientHandler()
				{
					Proxy = proxy,
					UseProxy = channelSettings.IsProxyEnabled(),
					Credentials = new NetworkCredential(serviceNowSettings.UserName, serviceNowSettings.Password),
				};
				using (HttpClient httpClient = new HttpClient(handler))
				{
					// Add an Accept header for JSON format.
					httpClient.DefaultRequestHeaders.Accept.Clear();
					httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					var fileStream = new ByteArrayContent(pdfFileStream.ToArray());
					fileStream.Headers.Remove("Content-Type");
					fileStream.Headers.Add("Content-Type", "application/octet-stream");
					fileStream.Headers.Add("Content-Transfer-Encoding", "binary");
					fileStream.Headers.Add("Content-Disposition", $"form-data;name=\"uploadFile\"; filename=\"{attachmentFileName}\"");
					var multipartContent = new MultipartFormDataContent();
					multipartContent.Add(new StringContent("incident"), "\"table_name\"");
					multipartContent.Add(new StringContent(sysId), "\"table_sys_id\"");
					multipartContent.Add(fileStream, "uploadFile");
					var response = httpClient.PostAsync(new Uri(attachFileUrl), multipartContent).Result;
					return response.IsSuccessStatusCode;
				}
			}
			catch (Exception ex)
			{
				LoggingHelper.Error($"Upload file to ServiceNow Incident was Unsuccessful. \n Response: {ex.Message}");
				throw ex;
			}
		}
		public ServiceNowSettings GetServiceNowProperties(List<CustomNotificationChannelSettings> serviceNowProperties, ServiceNowSettings serviceNowSettings)
		{
			foreach (var property in serviceNowProperties)
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
				if (property.Name.CaseInsensitiveEquals(Severity))
				{
					serviceNowSettings.Severity = GetServiceNowEventSeverity(property.Value, serviceNowSettings);
				}
			}
			return serviceNowSettings;
		}

		public EventSeverity GetServiceNowEventSeverity(string severity, ServiceNowSettings serviceNowSettings)
		{
			switch (severity)
			{
				case nameof(EventSeverity.clear):
					serviceNowSettings.Severity = EventSeverity.clear;
					break;
				case nameof(EventSeverity.critical):
					serviceNowSettings.Severity = EventSeverity.critical;
					break;
				case nameof(EventSeverity.major):
					serviceNowSettings.Severity = EventSeverity.major;
					break;
				case nameof(EventSeverity.minor):
					serviceNowSettings.Severity = EventSeverity.minor;
					break;
				case nameof(EventSeverity.warning):
					serviceNowSettings.Severity = EventSeverity.warning;
					break;
				case nameof(EventSeverity.ok):
					serviceNowSettings.Severity = EventSeverity.ok;
					break;
			}
			return serviceNowSettings.Severity;
		}

		public bool SendServiceNowEvent(Alarm alarm, string resource, string environmentName, string type, ServiceNowSettings serviceNowSettings, string eventDescription, WebProxy proxy, List<CustomNotificationChannel> channelSettings, bool isStatusCode, int severity)
		{
			resource = resource.TrimEnd(',');
			type = type.TrimEnd(',');
			eventDescription = JsonConvert.SerializeObject(eventDescription);
			string eventSeverity = severity.ToString();

			string messageKey = Guid.NewGuid().ToString();
			string eventMessage = string.Format("{{\"records\":[{{\"source\":\"BizTalk360 - {0}\",\n\"resource\": \"{1}\",\n\"node\": \"Environment Name - {2}\",\n\"metric_name\": \"\",\n\"type\": \"{3}\",\n\"severity\": \"{4}\",\n\"message_key\": \"{5}\",\n\"description\": {6}}}]}}", alarm.Name, resource, environmentName, type, eventSeverity, messageKey, eventDescription);
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
				HttpResponseMessage result = httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, string.Format("{0}/api/global/em/jsonv2", serviceNowSettings.ServiceNowUrl))
				{
					Headers =
																{
																		Authorization = new AuthenticationHeaderValue("Basic",
																		Convert.ToBase64String(
																				Encoding.ASCII.GetBytes($"{serviceNowSettings.UserName}:{serviceNowSettings.Password}")))
																},
					Content = new StringContent(eventMessage, Encoding.UTF8, "application/json")
				}).Result;
				if (result.IsSuccessStatusCode)
				{
					LoggingHelper.Info("ServiceNow Event Creation was Successful");
				}
				else
				{
					LoggingHelper.Error($"ServiceNow Event Creation was Unsuccessful. \n Response: {result}");
				}
				isStatusCode = result.IsSuccessStatusCode;
				return isStatusCode;
			}
		}


	}
}
