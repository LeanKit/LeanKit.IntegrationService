//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System.Net;
using ServiceStack.Common.Web;
using ServiceStack.ServiceInterface;

namespace IntegrationService.API
{
    public class ServiceBase:Service
    {
        public HttpResult NotAuthorized(string message = "")
        {
            return new HttpResult(HttpStatusCode.Unauthorized, message);
        }

        public HttpResult BadRequest(string message = "")
        {
            return new HttpResult(HttpStatusCode.BadRequest, message);
        }

        public HttpResult NotFound(string message = "")
        {
            return new HttpResult(HttpStatusCode.NotFound, message);
        }

        public object OK(object response)
        {
            return new HttpResult(response);
        }

        public object OK()
        {
            return new HttpResult(HttpStatusCode.OK);
        }

        public HttpResult ServerError(string message = "")
        {
            if (message.Contains("\r\n"))
                message = message.Replace("\r\n", " ");
            return new HttpResult(HttpStatusCode.InternalServerError, message);
        }

        public HttpResult Conflict(string message = "")
        {
            return new HttpResult(HttpStatusCode.Conflict, message);
        }
    }
}