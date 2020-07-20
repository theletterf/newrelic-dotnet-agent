﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class ServiceRequestAttributesLegacyDisabled : IClassFixture<RemoteServiceFixtures.WcfAppSelfHosted>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.WcfAppSelfHosted _fixture;

		public ServiceRequestAttributesLegacyDisabled([NotNull] RemoteServiceFixtures.WcfAppSelfHosted fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions
			(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(configPath);
					configModifier.ForceTransactionTraces();

					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "parameterGroups", "serviceRequestParameters" }, "enabled", "false");
				},
				exerciseApplication: () =>
				{
					_fixture.ReturnString();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var unexpectedTransactionTraceAttributes = new List<String>
			{
				"service.request.input",
			};

			var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
			Assert.NotNull(transactionSample);
			var matchedLogLine = _fixture.AgentLog.TryGetLogLine(@".*NewRelic WARN: Deprecated configuration property 'parameterGroups.serviceRequestParameters.enabled'.  Use 'attributes.exclude'.  See http://docs.newrelic.com for details.");

			NrAssert.Multiple
			(
				() => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.Agent, transactionSample),
				() => Assert.True(matchedLogLine != null, "Failed to locate deprecation warning log line.")
			);
		}
	}
}
