﻿using System;
using System.IO;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	/// <summary>
	/// Reuses the BasicWebApi application, because explicitly names itself "RuleWebApi" (instead of a random guid name) so that specific transaction renaming rules can be set up ahead of time to support URL rule tests.
	/// </summary>
	public class RulesWebApi : RemoteApplicationFixture
	{
		private const String ApplicationDirectoryName = @"BasicWebApi";
		private const String ExecutableName = @"BasicWebApi.exe";
		private const String TargetFramework = "net451";

		public RulesWebApi()
			: base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
		{
			Actions
			(
				setupConfiguration: () =>
				{
					var newRelicConfigFilePath = DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(newRelicConfigFilePath);

					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(newRelicConfigFilePath, new[] { "configuration", "log" }, "level", "debug");
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(newRelicConfigFilePath, new[] { "configuration", "requestParameters" }, "enabled", "true");

					var appConfigFilePath = Path.Combine(RemoteApplication.DestinationApplicationDirectoryPath, ExecutableName) + ".config";
					CommonUtils.SetAppNameInAppConfig(appConfigFilePath, "RulesWebApi");
				},
				exerciseApplication: () =>
				{
					Sleep();
					SegmentTerm();
					UrlRule();
				}
			);
		}

		public void Sleep()
		{
			var address = String.Format("http://{0}:{1}/api/Sleep", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("Great success", result);
		}

		public void SegmentTerm()
		{
			var address = String.Format("http://{0}:{1}/api/SegmentTerm", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("Great success", result);
		}

		public void UrlRule()
		{
			var address = String.Format("http://{0}:{1}/api/UrlRule", DestinationServerName, Port);
			var webClient = new WebClient();
			webClient.Headers.Add("accept", "application/json");

			var resultJson = webClient.DownloadString(address);
			var result = JsonConvert.DeserializeObject<String>(resultJson);

			Assert.Equal("Great success", result);
		}
	}
}
