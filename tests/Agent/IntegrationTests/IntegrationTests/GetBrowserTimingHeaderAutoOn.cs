﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class GetBrowserTimingHeaderAutoOn : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

		private String _browserTimingHeader;
		private String _htmlContentAfterCallToGetBrowserTiming;

		public GetBrowserTimingHeaderAutoOn([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;

					var configModifier = new NewRelicConfigModifier(configPath);
					configModifier.AutoInstrumentBrowserMonitoring(true);
					configModifier.BrowserMonitoringEnableAttributes(true);

				},
				exerciseApplication: () =>
				{
					_browserTimingHeader = _fixture.GetBrowserTimingHeader();
					_htmlContentAfterCallToGetBrowserTiming = _fixture.GetHtmlWithCallToGetBrowserTimingHeader();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			NrAssert.Multiple(
				() => Assert.Contains("NREUM", _browserTimingHeader),
				ShouldNotAutoInstrumentAfterCallToGetBrowserTimingHeader
			);

			var browserMonitoringConfig = JavaScriptAgent.GetJavaScriptAgentConfigFromSource(_browserTimingHeader);

			NrAssert.Multiple(
				() => Assert.Contains("beacon", browserMonitoringConfig.Keys),
				() => Assert.Contains("errorBeacon", browserMonitoringConfig.Keys),
				() => Assert.Contains("licenseKey", browserMonitoringConfig.Keys),
				() => Assert.Contains("applicationID", browserMonitoringConfig.Keys),
				() => Assert.Contains("transactionName", browserMonitoringConfig.Keys),
				() => Assert.Contains("queueTime", browserMonitoringConfig.Keys),
				() => Assert.Contains("applicationTime", browserMonitoringConfig.Keys),
				() => Assert.Contains("agent", browserMonitoringConfig.Keys),
				() => Assert.Contains("atts", browserMonitoringConfig.Keys)
			);

			var attrsDict = HeaderEncoder.DecodeAndDeserialize<Dictionary<string, IDictionary<String, Object>>>(browserMonitoringConfig["atts"], _fixture.TestConfiguration.LicenseKey, 13);
			Assert.Contains("a", attrsDict.Keys);
			IDictionary<string, Object> agentAttrsDict = attrsDict["a"];
			Assert.Contains("nr.tripId", agentAttrsDict.Keys);

			NrAssert.Multiple(
				() => Assert.NotNull(browserMonitoringConfig["beacon"]),
				() => Assert.NotNull(browserMonitoringConfig["errorBeacon"]),
				() => Assert.NotNull(browserMonitoringConfig["licenseKey"]),
				() => Assert.NotNull(browserMonitoringConfig["applicationID"]),
				() => Assert.NotNull(browserMonitoringConfig["transactionName"]),
				() => Assert.NotNull(browserMonitoringConfig["queueTime"]),
				() => Assert.NotNull(browserMonitoringConfig["applicationTime"]),
				() => Assert.NotNull(browserMonitoringConfig["agent"])
			);
		}

		private void ShouldNotAutoInstrumentAfterCallToGetBrowserTimingHeader()
		{
			Assert.DoesNotContain("NREUM", _htmlContentAfterCallToGetBrowserTiming);
		}
	}
}
