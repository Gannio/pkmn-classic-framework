﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Text;
using Newtonsoft.Json;
using PkmnFoundations.Data;

namespace PkmnFoundations.Web
{
    /// <summary>
    /// Summary description for ForeignLookupSource
    /// </summary>
    public class ForeignLookupSource : IHttpHandler, System.Web.SessionState.IRequiresSessionState
    {
        public ForeignLookupSource()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        public void ProcessRequest(HttpContext context)
        {
            if (String.IsNullOrEmpty(context.Request.Form["n"])
                || String.IsNullOrEmpty(context.Request.Form["q"])
                || String.IsNullOrEmpty(context.Request.Form["c"]))
            {
                ServerError(context);
                return;
            }

            int rows = 0;
            if (!Int32.TryParse(context.Request.Form["n"], out rows))
            {
                ServerError(context);
                return;
            }
            if (rows < 0)
            {
                ServerError(context);
                return;
            }

            String control_id = context.Request.Form["c"];
            if (control_id.Contains('<') || control_id.Contains('>') || control_id.Contains('\"')
                || control_id.Contains('\'') || control_id.Contains('\\'))
            {
                ServerError(context);
                return;
            }

            bool french = (context.Request.QueryString["lang"] ?? "EN").ToUpperInvariant() == "FR";
            String format = context.Request.Form["f"] ?? "h";

            if (!new[]{"h", "j"}.Contains(format))
            {
                ServerError(context);
                return;
            }

            DataTable data;
            try
            {
                data = GetData(context.Request.Form["q"], rows, french);
            }
            catch (Exception ex)
            {
                // todo: log error
                ServerError(context);
                return;
            }

            if (data == null || data.Rows.Count == 0)
            {
                EmptyResult(context, format);
                return;
            }

            if (!data.Columns.Contains("Text") || !data.Columns.Contains("Value"))
            {
                ServerError(context);
                return;
            }

            switch (format)
            {
                case "h":
                    WriteHtml(context, data, control_id);
                    break;
                case "j":
                    WriteJson(context, data);
                    break;
                default:
                    // unreachable
                    break;
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        private void ServerError(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            context.Response.Write("Bad request");
            context.Response.StatusCode = 400;
        }

        private void EmptyResult(HttpContext context, String format)
        {
            switch (format)
            {
                case "h":
                    context.Response.ContentType = "text/plain";
                    context.Response.Write("No results");
                    break;
                case "j":
                    context.Response.ContentType = "text/javascript";
                    context.Response.Write("[]");
                    break;
                default:
                    // unreachable
                    break;
            }
        }

        private void WriteHtml(HttpContext context, DataTable data, String control_id)
        {
            context.Response.ContentType = "text/plain";

            StringBuilder builder = new StringBuilder();
            int index = 0;
            foreach (DataRow row in data.Rows)
            {
                String text = Common.HtmlEncode(row["Text"].ToString());
                // this breaks if value contains html control chars but is fine since all values will be int
                String value = Common.HtmlEncode(row["Value"].ToString());

                builder.Remove(0, builder.Length);
                builder.Append("<div class=\"result\" ");

                if (index == 0)
                {
                    builder.Append("id=\"");
                    builder.Append(control_id);
                    builder.Append("_result1\" ");
                }

                builder.Append("data-value=\"");
                builder.Append(Common.HtmlEncode(value));
                builder.Append("\" data-text=\"");
                builder.Append(Common.HtmlEncode(text));
                builder.Append("\" onclick=\"iaSelectResult('");
                builder.Append(control_id);
                builder.Append("_main', '"); // slight hack to avoid passing all client ids in here
                builder.Append(control_id);
                builder.Append("_hdSelectedValue', '");
                builder.Append(Common.HtmlEncode(Common.JsEncode(value)));
                builder.Append("', '");
                builder.Append(control_id);
                builder.Append("_txtInput', '");
                builder.Append(Common.HtmlEncode(Common.JsEncode(text)));
                builder.Append("')\">");
                builder.Append(Common.HtmlEncode(text));
                builder.Append("</div>\n");

                context.Response.Write(builder.ToString());
                index++;
            }
        }

        private void WriteJson(HttpContext context, DataTable data)
        {
            context.Response.ContentType = "text/javascript";

            ForeignLookupResult[] results = new ForeignLookupResult[data.Rows.Count];
            for (int x = 0; x < results.Length; x++)
            {
                DataRow row = data.Rows[x];
                results[x] = new ForeignLookupResult 
                { 
                    t = DatabaseExtender.Cast<String>(row["Text"]) ?? "", 
                    v = DatabaseExtender.Cast<int ?>(row["Value"]) ?? 0 };
            }
            context.Response.Write(JsonConvert.SerializeObject(results));
        }

        protected virtual DataTable GetData(String query, int rows, bool french)
        {
            return null;
        }
    }

    internal class ForeignLookupResult
    {
        public String t;
        public int v;
    }
}