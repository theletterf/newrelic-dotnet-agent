/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using OpenTracing.Util;
using NewRelic.OpenTracing.AmazonLambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsLambdaErrorTestFunction
{
    public class Function
    {

        public Function()
        {
            GlobalTracer.Register(LambdaTracer.Instance);
        }

        public APIGatewayProxyResponse FunctionWrapper(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return new TracingRequestHandler().LambdaWrapper(FunctionHandler, request, context);
        }

        APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            throw new Exception("my exception");
        }
    }
}
