namespace Search.Dialogs
{
    using System.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Connector;
    using Search.Models;
    using Search.Services;
    using System.Diagnostics;
    using System.IO;
    using Newtonsoft.Json;
    using System.Net.Http;
    using System.Net;
    using System.Net.Http.Headers;


    [Serializable]
    public abstract partial class SearchDialog : IDialog<IList<SearchHit>>
    {
        protected readonly ISearchClient SearchClient;
        protected readonly SearchQueryBuilder QueryBuilder;
        protected readonly PromptStyler HitStyler;
        protected readonly bool MultipleSelection;
        private readonly IList<SearchHit> selected = new List<SearchHit>();

        private bool firstPrompt = true;
        private IList<SearchHit> found;
        //private string fullpathGoo = "C:\\temp\\bot\\g1.py";
        //private string fullpathWiki = "C:\\temp\\bot\\w1.py";
        //private string fullpathYah = "C:\\temp\\bot\\y1.py";
        //private string fullpathboo = "C:\\temp\\bot\\b1.py";

        [NonSerialized]
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        bool debug = true;

        public SearchDialog(ISearchClient searchClient, SearchQueryBuilder queryBuilder = null, PromptStyler searchHitStyler = null, bool multipleSelection = false)
        {
            SetField.NotNull(out this.SearchClient, nameof(searchClient), searchClient);

            this.QueryBuilder = queryBuilder ?? new SearchQueryBuilder();
            this.HitStyler = searchHitStyler ?? new SearchHitStyler();
            this.MultipleSelection = multipleSelection;
        }

        public Task StartAsync(IDialogContext context)
        {
            if (debug)
            {
                var data = context.UserData;
                data.RemoveValue("userid");
                data.RemoveValue("attempts");
                data.RemoveValue("session");
            }

            return this.PromptRouter(context);
        }

        protected virtual async Task PromptRouter(IDialogContext context)
        {
            var data = context.UserData;
            if (!data.ContainsKey("userid"))
            {
                await this.PromptUserId(context);
            }
            else if (!data.ContainsKey("session"))
            {
                await this.PromptSessionChoices(context);
            }
            else if (data.GetValue<string>("session") == "simple")
            {
                //await this.DisplayInformation(context);
                await this.StartPractice(context);
            }
            else if (data.GetValue<string>("session") == "challenge")
            {
                //await this.DisplayInformation(context);
                await this.StartChallenge(context);
            }

        }

        async Task ShouldContinueOps(IDialogContext context, IAwaitable<string> result)
        {
            await this.PromptRouter(context);
        }
        async Task ShouldContinueOps(IDialogContext context)
        {
            await this.PromptRouter(context);
        }

        async Task DisplayInformation(IDialogContext context)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLineWithMarkdown(string.Format("Userid  = {0}", context.UserData.GetValueOrDefault<string>("userid", null)));
            sb.AppendLineWithMarkdown(string.Format("Session = {0}", context.UserData.GetValueOrDefault<string>("session", null)));
            await context.PostAsync(sb.ToString());
        }

        Task PromptUserId(IDialogContext context)
        {
            var data = context.UserData;
            if (data.GetValueOrDefault("attempts", 0) == 0)
            {
                PromptDialog.Text(context, this.HandleUserIdResponseAsync, Conversations.TellMeYourId);
            }
            else
            {
                PromptDialog.Text(context, this.HandleUserIdResponseAsync, Conversations.TellMeYourIdAgain);
            }

            return Task.CompletedTask;
        }

        async Task HandleUserIdResponseAsync(IDialogContext context, IAwaitable<string> result)
        {
            var value = (await result).ToLower();
            if (string.IsNullOrEmpty(value) || value.Contains(" "))
            {
                var data = context.UserData;
                int attempts = data.GetValueOrDefault<int>("attempts", 0);
                data.SetValue("attempts", ++attempts);

                await context.PostAsync(Conversations.ERROR_INVALID_USERID);
                await this.ShouldContinueOps(context);
            }
            else
            {
                var data = context.UserData;
                data.SetValue("userid", value);
                await context.PostAsync(string.Format("Thanks, we have added **_{0}_** as your id :)", value));
                await this.ShouldContinueOps(context);
            }
        }

        private static int retries = 100;

        Task PromptSessionChoices(IDialogContext context)
        {
            var options = new List<string>() { "Um, I just want some practice :)", "Alright, challenge accepted!" };
            PromptDialog.Choice<string>(context,
                this.HandleSessionResponse,
                options,
                Conversations.ChooseYourSession,
                Conversations.ChooseYourSession,
                retries);
            return Task.CompletedTask;
        }

        private async Task HandleSessionResponse(IDialogContext context, IAwaitable<string> result)
        {
            var response = (await result);

            string code = string.Empty;
            string message = string.Empty;

            if (response.Contains("practice"))
            {
                code = "simple";
                message = "Alright, let's try the basics first.";
            }
            else
            {
                code = "challenge";
                message = string.Format("Awesome! You are now a challenger ;)");
            }

            var data = context.UserData;
            data.SetValue("session", code);

            await context.PostAsync(message);
            await this.ShouldContinueOps(context);
        }


        #region Challenge and Practice

        Task StartPractice(IDialogContext context)
        {
            var data = context.UserData;
            if (data.GetValueOrDefault<string>("userid", null) == null)
            {
                return this.ShouldContinueOps(context);
            }

            PromptDialog.Text(context, this.HandlePracticeResponse, Conversations.PleaseEnterPracticeRequest);

            return Task.CompletedTask;
        }

        Task StartChallenge(IDialogContext context)
        {
            var data = context.UserData;
            if (data.GetValueOrDefault<string>("userid", null) == null)
            {
                return this.ShouldContinueOps(context);
            }

            PromptDialog.Text(context, this.HandleChallengeResponse, Conversations.PleaseEnterTickerInformation);

            return Task.CompletedTask;
        }

        const string challengeUri = "http://139.59.231.81:5000/api/challenge/{0}?params={1}";
        const string practiceUri = "http://139.59.231.81:5000/api/practice/{0}?params={1}";

        async Task HandleChallengeResponse(IDialogContext context, IAwaitable<string> result)
        {
            await this.HandleGenericResponse(context, result, challengeUri);
        }

        async Task HandlePracticeResponse(IDialogContext context, IAwaitable<string> result)
        {
            await this.HandleGenericResponse(context, result, practiceUri);
        }

        async Task HandleGenericResponse(IDialogContext context, IAwaitable<string> result, string uri)
        {
            var data = context.UserData;
            string userid = data.GetValueOrDefault<string>("userid", null);
            if (userid != null)
            {
                logger.Info("starting response");
                var ticker = (await result).ToUpper();
                string queryUri = string.Format(uri, userid, ticker);
                if (!await RunGenericPyfetch(queryUri, context))
                {
                    await this.ShouldContinueOps(context);
                };
            }
        }

        async Task<bool> RunGenericPyfetch(string pyUri, IDialogContext context)
        {
            logger.Info($"{pyUri}");
            try
            {
                string content = string.Empty;

                if (!debug)
                {
                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = await httpClient.GetAsync(pyUri);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        await context.PostAsync(Conversations.ERROR_CANNOT_CONNECT_TO_PYTHONAPI);
                        return false;
                    }
                    content = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    content = mockAPIResponse();
                }

                var resultList = JsonConvert.DeserializeObject<List<RootObject>>(content);
                if (resultList.Count() > 0)
                {
                    parseResultSet(resultList, context);
                    return true;
                }
            }
            catch (Exception e)
            {
                PromptDialog.Text(context, this.ShouldContinueOps, e.Message);
            }

            return false;
        }

        void parseResultSet(List<RootObject> roots, IDialogContext context)
        {
            StringBuilder sb = new StringBuilder();

            foreach (RootObject root in roots)
            {
                var data = root.data;
                if (root.data != null)
                {
                    if (root.type.Equals("string"))
                    {
                        if (!string.IsNullOrWhiteSpace(data))
                        {
                            sb.AppendLineWithMarkdown(data);
                        }
                    }
                    else if (root.type.Equals("kvpair"))
                    {
                        string[][] kvpairs = JsonConvert.DeserializeObject<string[][]>(data);

                        string[] keys = kvpairs[0];
                        string[] vals = kvpairs[1];

                        //int maxLength = 0;
                        //foreach (string k in keys)
                        //{
                        //    maxLength = k.Length > maxLength ? k.Length : maxLength;
                        //}
                        //sb.AppendLineWithMarkdown("<table><tr><td>dacasca</td></tr></table>");

                        for (int c = 0; c < keys.Length; c++)
                        {
                            //string padd = string.Concat(Enumerable.Repeat("&nbsp;", maxLength - keys[c].Length));
                            sb.AppendLineWithMarkdown(string.Format("* **{0}**: {1}", keys[c], vals[c]));
                        }
                    }
                    else if (root.type.Equals("link"))
                    {
                        string[] link = JsonConvert.DeserializeObject<string[]>(data);

                        if (link.Length >= 2 && link.Length <= 3)
                        {
                            string format = (link.Length == 3) ? link[2] : "[{0}]({1})";
                            sb.AppendLineWithMarkdown(string.Format(format, link[1], link[0]));
                        }
                    }
                    else if (root.type.Equals("image"))
                    {
                        if (!string.IsNullOrWhiteSpace(data))
                        {
                            sb.AppendLineWithMarkdown(string.Format("![alt]({0})", data));
                        }
                    }
                }
            }

            string chatmsg = sb.ToString();
            if (!string.IsNullOrWhiteSpace(chatmsg))
            {
                PromptDialog.Text(context, this.ShouldContinueOps, chatmsg);
            }
            else
            {
                PromptDialog.Text(context, this.ShouldContinueOps, "Check your script again.. data seems to be empty :)");
            }
        }

        #endregion

        string mockAPIResponse()
        {
            List<RootObject> roots = new List<RootObject>();
            roots.Add(new RootObject { type = "string", data = "Search results from Yahoo finance" });

            string[] link = new string[] { "https://finance.yahoo.com/quote/AAPL?p=AAPL", "Yahoo Finance" };
            roots.Add(new RootObject { type = "link", data = JsonConvert.SerializeObject(link) });

            string[] keys = new string[] { "Stock Price", "Beta", "PE Ratio" };
            string[] vals = new string[] { "val3", "val2", "value" };
            string[][] kvpairs = new string[][] { keys, vals };
            roots.Add(new RootObject { type = "kvpair", data = JsonConvert.SerializeObject(kvpairs) });

            roots.Add(new RootObject { type = "image", data = "https://cdn3.iconfinder.com/data/icons/picons-social/57/56-apple-256.png" });

            return JsonConvert.SerializeObject(roots);
        }

    }

}