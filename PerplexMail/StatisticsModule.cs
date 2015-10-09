﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Net.Mail;

namespace PerplexMail
{
    /// <summary>
    /// This module tracks any events (statistics) that are triggered from e-mails send by the e-mailpackage.
    /// </summary>
    public class HttpStatisticsModule : IHttpModule
    {
        /// <summary>
        /// This method is called by underlying code and should not be called directly.
        /// </summary>
        /// <param name="context">The application context</param>
        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(context_BeginRequest);
        }

        /// <summary>
        /// This method is triggered for EVERY request the website gets. This includes html files, images, stylesheets etc.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void context_BeginRequest(object sender, EventArgs e)
        {
            HttpContext c = ((HttpApplication)sender).Context;
            // Determine if the request has special variables ment for this statistics module
            if (c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_MAILID] != null && c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_ACTION] != null)
                // Register event
                processMailStatistics(c);
        }

        /// <summary>
        /// This method processes e-mail statistics
        /// </summary>
        /// <param name="c">The context of the request</param>
        void processMailStatistics(HttpContext c)
        {
            int id;
            if (int.TryParse(c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_MAILID], out id) && id > 0 && // Check if a valid email ID has been specified in the querystring parameter
                !String.IsNullOrEmpty(c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_ACTION])) // Check if a valid action has been specified in the querystring parameter
            {
                // Note: we do not validate the value querystring paramter as this is optional
                // Determine the action type triggered in the e-mail
                EnmAction action;
                if (Enum.TryParse<EnmAction>(c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_ACTION], out action))
                    switch (action)
                    {
                        case EnmAction.view:
                            // The e-mail has been viewed by the user. This occurs when images are loaded in the email (if supported)
                            registerEvent(c);
                            break;
                        case EnmAction.click:
                            // The user has clicked on a link in the email.
                            registerEvent(c);
                            // Redirect to the final destination
                            c.Response.Redirect(c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_VALUE], false);
                            c.ApplicationInstance.CompleteRequest();
                            break;
                        case EnmAction.webversion:
                            // The user wants to view a webversion of the email.
                            // Confirm the authentication hash is present
                            if (!String.IsNullOrEmpty(c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_AUTH]))
                            {
                                // Determine if the requested email exists
                                string exists = PerplexMail.Sql.ExecuteSql("SELECT COUNT(*) FROM [" + Constants.SQL_TABLENAME_PERPLEXMAIL_LOG + "] WHERE [id] = @id", System.Data.CommandType.Text, new SqlParameter("@id", +id));
                                if (exists == "1" && // Does the email exist?
                                    c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_AUTH] == Security.GenerateHash(id.ToString())) // Check the authentication hash
                                {
                                    // Register the webversion view in our database statistics table
                                    registerEvent(c);

                                    // Load the email from the log
                                    var email = LogEmail.Get(id);

                                    // We do not want the preview to count as a view/statistic. Remove statistic tags from the body
                                    email.body = Helper.RemoveMailTag(email.body);

                                    // Send the email to the user
                                    sendResponse(c.Response, 200, email.body);
                                }
                                else
                                    sendResponse(c.Response, 401);  // Unauthorized
                            }
                            else
                                sendResponse(c.Response, 401); // Unauthorized
                            break;
                        default:
                            // Ongeldige actie
                            sendResponse(c.Response, 400);
                            break;
                    }
            }
        }

        void registerEvent(HttpContext c)
        {
            // Indien het NIET een [p]review betreft dan mogen we hem tracken. Als je dus het mailtje bekijkt in Umbraco dan telt de stat niet mee!
            if (c.Request["p"] != "1")
            {
                bool retry = true;
            retry:
                try
                {
                    string useragent = c.Request.UserAgent;
                    if (useragent.Length > 255)
                        useragent = useragent.Substring(0, 255);
                    var parameters = new List<SqlParameter>();
                    parameters.Add(new SqlParameter("emailID", c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_MAILID]));
                    parameters.Add(new SqlParameter("action", c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_ACTION]));
                    var value = c.Request[Constants.STATISTICS_QUERYSTRINGPARAMETER_VALUE];
                    if (!String.IsNullOrEmpty(value))
                        parameters.Add(new SqlParameter("value", value));
                    else
                        parameters.Add(new SqlParameter("value", DBNull.Value));
                    parameters.Add(new SqlParameter("ip", c.Request.UserHostAddress));
                    parameters.Add(new SqlParameter("useragent", useragent));
                    PerplexMail.Sql.ExecuteSql(Constants.SQL_QUERY_ADD_STATISTICS, System.Data.CommandType.Text, parameters.ToArray());
                }
                catch (SqlException ex)
                {
                    if (retry && Helper.HandleSqlException(ex))
                    {
                        retry = false;
                        // Controleer of de benodigde dingen in de database staan (Tabellen, stored procedures)
                        goto retry;
                    }
                    else
                        throw;
                }
            }
        }

        /// <summary>
        /// Directly sends a response to the user and terminates the current request
        /// </summary>
        /// <param name="r">The response that is to be sent to the user</param>
        /// <param name="statusCode">The status code, for example 200 or 404 or 500</param>
        /// <param name="content">Any relevant content that is to be sent to the user</param>
        /// <param name="ContentType">The type of the content that is to be sent to the user. Default is text/html</param>
        void sendResponse(HttpResponse r, int statusCode, string content = "", string ContentType = "text/html")
        {
            r.Clear();
            r.StatusCode = statusCode;
            if (!String.IsNullOrEmpty(content) && !String.IsNullOrEmpty(ContentType))
            {
                r.ContentType = ContentType;
                r.Write(content);
            }
            r.End();
        }

        public void Dispose() { }
    }
}