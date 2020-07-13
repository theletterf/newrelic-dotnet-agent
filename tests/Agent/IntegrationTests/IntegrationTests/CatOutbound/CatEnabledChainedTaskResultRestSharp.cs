/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CatOutbound
{
    [NetFrameworkTest]
    public class CatEnabledChainedTaskResultRestSharp : IClassFixture<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;


        private HttpResponseHeaders _responseHeaders;

        public CatEnabledChainedTaskResultRestSharp(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration" }, "crossApplicationTracingEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "crossApplicationTracer" }, "enabled", "true");

                },
                exerciseApplication: () =>
                {
                    _fixture.GetIgnored();
                    _responseHeaders = _fixture.GetRestSharpTaskResultClientWithHeaders(method: "GET", generic: true, cancelable: true);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        [Trait("feature", "CAT-DistributedTracing")]
        public void Test()
        {
            var catResponseHeader = _responseHeaders.GetValues(@"X-NewRelic-App-Data")?.FirstOrDefault();
            Assert.NotNull(catResponseHeader);

            var catResponseData = HeaderEncoder.DecodeAndDeserialize<CrossApplicationResponseData>(catResponseHeader, HeaderEncoder.IntegrationTestEncodingKey);

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var calleeTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/RestSharpController/TaskResultClient");
            Assert.NotNull(calleeTransactionEvent);

            var myHostname = _fixture.DestinationServerName;

            var crossProcessId = _fixture.AgentLog.GetCrossProcessId();

            // Note: we are checking the metrics that are generated by the *Caller* as a result of receiving a CAT response.
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"External/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"External/{myHostname}/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"ExternalApp/{myHostname}/{crossProcessId}/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"ExternalTransaction/{myHostname}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Get", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"ExternalTransaction/{myHostname}/{crossProcessId}/WebTransaction/WebAPI/RestAPI/Get", metricScope = @"WebTransaction/MVC/RestSharpController/TaskResultClient", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"External/{myHostname}/Stream/GET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"ClientApplication/[^/]+/all", IsRegexName = true }
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// This scoped metric should be superceded by the ExternalTransaction metric above
				new Assertions.ExpectedMetric { metricName = $@"External/{_fixture.RemoteApplication.DestinationServerName}/Stream/GET", metricScope = @"WebTransaction/MVC/RestSharpController/TaskResultClient" }
            };

            // Note: we are checking the attributes attached to the *Callee's* transaction, not the caller's transaction. The attributes attached to the caller's transaction are already fully vetted in the CatInbound tests.
            var expectedTransactionEventIntrinsicAttributes1 = new List<string>
            {
                "nr.guid",
                "nr.pathHash",
                "nr.referringPathHash",
                "nr.referringTransactionGuid"
            };
            var expectedTransactionEventIntrinsicAttributes2 = new Dictionary<string, string>
            {
				// This value comes from what we send to the application (see parameter passed to GetWithCatHeader above)
				{"nr.tripId", "tripId"}
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/MVC/RestSharpController/TaskResultClient" || sample.Path == @"WebTransaction/WebAPI/RestAPI/Get")
                .FirstOrDefault();

            Assert.NotNull(transactionSample);


            NrAssert.Multiple(
                () => Assert.Equal(crossProcessId, catResponseData.CrossProcessId),
                () => Assert.Equal("WebTransaction/MVC/RestSharpController/TaskResultClient", catResponseData.TransactionName),
                () => Assert.True(catResponseData.QueueTimeInSeconds >= 0),
                () => Assert.True(catResponseData.ResponseTimeInSeconds >= 0),
                () => Assert.Equal(-1, catResponseData.ContentLength),
                () => Assert.NotNull(catResponseData.TransactionGuid),
                () => Assert.False(catResponseData.Unused),

                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),

                // calleeTransactionEvent attributes
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes1, TransactionEventAttributeType.Intrinsic, calleeTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes2, TransactionEventAttributeType.Intrinsic, calleeTransactionEvent)
            );
        }
    }
}