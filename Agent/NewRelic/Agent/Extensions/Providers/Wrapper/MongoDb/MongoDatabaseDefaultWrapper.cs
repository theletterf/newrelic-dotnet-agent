﻿using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.MongoDb
{
	public class MongoDatabaseDefaultWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoDatabase",
				methodName: "CreateCollection", parameterSignature: "System.String,MongoDB.Driver.IMongoCollectionOptions");
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
			var model = GetCollectionName(instrumentedMethodCall.MethodCall);
			var segment = transactionWrapperApi.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation));

			return Delegates.GetDelegateFor(segment);
		}

		private String GetCollectionName(MethodCall methodCall)
		{
			return methodCall.MethodArguments.ExtractNotNullAs<String>(0);
		}
	}
}
