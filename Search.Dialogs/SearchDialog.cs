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
    public abstract class SearchDialog : IDialog<IList<SearchHit>>
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

        protected virtual Task PromptRouter(IDialogContext context)
        {
            var data = context.UserData;
            if (!data.ContainsKey("userid"))
            {
                this.PromptUserId(context);
            }
            else if (!debug && !data.ContainsKey("session"))
            {
                //this.PromptSessionChoices(context);
            }
            else if (!debug && data.GetValue<string>("session") == "simple")
            {
                //this.StartSimpleTutorial(context);
            }
            else if (debug || data.GetValue<string>("session") == "challenge")
            {
                this.StartChallenge(context);
            }

            return Task.CompletedTask;
        }

        async Task ShouldContinueOps(IDialogContext context, IAwaitable<string> result)
        {
            await this.PromptRouter(context);
        }
        async Task ShouldContinueOps(IDialogContext context)
        {
            await this.PromptRouter(context);
        }

        void PromptUserId(IDialogContext context)
        {
            var data = context.UserData;
            if (data.GetValueOrDefault("attempts", 0) ==  0)
            {
                PromptDialog.Text(context, this.ReceiveUserIdAsync, Conversations.TellMeYourId);
            }
            else
            {
                PromptDialog.Text(context, this.ReceiveUserIdAsync, Conversations.TellMeYourIdAgain);
            }
        }

        async Task ReceiveUserIdAsync(IDialogContext context, IAwaitable<string> result)
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
                await context.PostAsync(string.Format("Thanks, we have added '{0}' as your id :)", value));
                await this.ShouldContinueOps(context);
            }

        }

        #region Challenge

        Task StartChallenge(IDialogContext context)
        {
            var data = context.UserData;
            if (data.GetValueOrDefault<string>("userid", null) == null)
            {
                return this.ShouldContinueOps(context);
            }

            PromptDialog.Text(context, this.HandleChallengeResponse, "Enter the TICKR for info to be scraped");

            return Task.CompletedTask;
        }

        const string challengeUri = "http://139.59.231.81:5000/api/challenge/{0}?params={1}";

        private async Task HandleChallengeResponse(IDialogContext context, IAwaitable<string> result)
        {
            var data = context.UserData;
            string userid = data.GetValueOrDefault<string>("userid", null);
            if (userid != null)
            {
                var ticker = (await result).ToUpper();
                try
                {
                    logger.Info("starting response");
                    string queryUri = string.Format(challengeUri, userid, ticker);
                    await RunGenericPyfetch(queryUri, context);
                }
                catch (Exception e)
                {
                    PromptDialog.Text(context, this.ShouldContinueOps, "Fail whale ... " + e.Message);
                }

            }
        }

        private async Task RunGenericPyfetch(string pyUri, IDialogContext context)
        {
            logger.Info($"{pyUri}");
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.GetAsync(pyUri);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new IOException("Unable to reach the server");
                }

                var content = await response.Content.ReadAsStringAsync();

                var resultList = JsonConvert.DeserializeObject<List<RootObject>>(content);
                if (resultList.Count() > 0)
                {
                    parseResultSet(resultList, context);

                    //var resultTwo = resultList[0];
                    //if (resultTwo.type.Equals("cards"))
                    //{

                    //    var cardData = JsonConvert.DeserializeObject<List<RootCardObject>>(resultTwo.data.ToString());
                    //    if (cardData.Any())
                    //    {
                    //        var cards = new List<ThumbnailCard>();
                    //        foreach (var s in cardData)
                    //        {
                    //            var card = new ThumbnailCard
                    //            {
                    //                Title = s.title,
                    //                Images = new[] { new CardImage(s.img) },
                    //                Text = s.description
                    //            };
                    //            cards.Add(card);
                    //        }
                    //        var message = context.MakeMessage();
                    //        message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    //        message.Attachments = cards.Select(p => p.ToAttachment()).ToList();
                    //        context.PostAsync(message);
                    //        PromptDialog.Text(context, this.ShouldContinueOps, resultList[0].data.ToString());
                    //    }
                    //    else
                    //    {
                    //        PromptDialog.Text(context, this.ShouldContinueOps, "Oops .. No results here .. try something else!");
                    //    }
                    //}
                    //else
                    //{
                    //    var restext = resultList[0].data.ToString();
                    //    if (!string.IsNullOrEmpty(restext))
                    //    {
                    //        PromptDialog.Text(context, this.ShouldContinueOps, restext);
                    //    }
                    //    else
                    //    {
                    //        PromptDialog.Text(context, this.ShouldContinueOps, "Check your script again.. data seems to be empty :)");
                    //    }
                    //}

                }
            }
            catch (Exception e)
            {
                PromptDialog.Text(context, this.ShouldContinueOps, "Something quite wrong with the way you are doing things.. here's a very technical exception :P " + e.Message);
            }

        }

        void parseResultSet(List<RootObject> roots, IDialogContext context)
        {
            StringBuilder sb = new StringBuilder();

            foreach (RootObject root in roots)
            {
                if (root.type.Equals("string"))
                {
                    var restext = root.data?.ToString();

                    if (!string.IsNullOrWhiteSpace(restext))
                    {
                        sb.AppendLine(restext);
                    }
                }
                //else if (root.type.Equals("cards"))
                //{

                //}
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

        public async Task Search(IDialogContext context, IAwaitable<string> input)
        {
            string text = input != null ? await input : null;
            if (this.MultipleSelection && text != null && text.ToLowerInvariant() == "list")
            {
                await this.ListAddedSoFar(context);
                await this.PromptRouter(context);
            }
            else
            {
                if (text != null)
                {
                    this.QueryBuilder.SearchText = text;
                }

                var response = await this.ExecuteSearchAsync();

                if (response.Results.Count() == 0)
                {
                    await this.NoResultsConfirmRetry(context);
                }
                else
                {
                    var message = context.MakeMessage();
                    this.found = response.Results.ToList();
                    this.HitStyler.Apply(
                        ref message,
                        "Here are a few good options I found:",
                        this.found.ToList().AsReadOnly());
                    await context.PostAsync(message);
                    await context.PostAsync(
                        this.MultipleSelection ?
                        "You can select one to buy sell or know more" :
                        "You can select one, *refine* these results, see *more* or search *again*.");
                    context.Wait(this.ActOnSearchResults);
                }
            }
        }




        protected virtual Task NoResultsConfirmRetry(IDialogContext context)
        {
            PromptDialog.Confirm(context, this.ShouldRetry, "Sorry, I didn't find any matches. Do you want to retry your search?");
            return Task.CompletedTask;
        }

        protected virtual async Task ListAddedSoFar(IDialogContext context)
        {
            var message = context.MakeMessage();
            if (this.selected.Count == 0)
            {
                await context.PostAsync("You have not added anything yet.");
            }
            else
            {
                this.HitStyler.Apply(ref message, "Here's what you've added to your list so far.", this.selected.ToList().AsReadOnly());
                await context.PostAsync(message);
            }
        }

        protected virtual async Task AddSelectedItem(IDialogContext context, string selection)
        {
            SearchHit hit = this.found.SingleOrDefault(h => h.Key == selection);
            if (hit == null)
            {
                await this.UnkownActionOnResults(context, selection);
            }
            else
            {
                if (!this.selected.Any(h => h.Key == hit.Key))
                {
                    this.selected.Add(hit);
                }

                if (this.MultipleSelection)
                {
                    await context.PostAsync($"'{hit.Title}' was selected");
                    PromptDialog.Choice<string>(context, this.ShouldContinueSearching, new List<string>() { "BUY", "SELL", "KNOW MORE" }, "Please select one of the above options");
                }
                else
                {
                    context.Done(this.selected);
                }
            }
        }

        protected virtual async Task UnkownActionOnResults(IDialogContext context, string action)
        {
            await context.PostAsync("Not sure what you mean. You can search *again*, *refine*, *list* or select one of the items above. Or are you *done*?");
            context.Wait(this.ActOnSearchResults);
        }

        protected virtual async Task ShouldContinueSearching(IDialogContext context, IAwaitable<string> input)
        {
            try
            {
                string shouldContinue = await input;
                if (shouldContinue.Equals("BUY"))
                {
                    await this.PromptRouter(context);
                }
                else
                {
                    context.Done(this.selected);
                }
            }
            catch (TooManyAttemptsException)
            {
                context.Done(this.selected);
            }
        }

        protected void SelectRefiner(IDialogContext context)
        {
            var dialog = new SearchSelectRefinerDialog(this.GetTopRefiners(), this.QueryBuilder);
            context.Call(dialog, this.Refine);
        }

        protected async Task Refine(IDialogContext context, IAwaitable<string> input)
        {
            string refiner = await input;

            if (!string.IsNullOrWhiteSpace(refiner))
            {
                var dialog = new SearchRefineDialog(this.SearchClient, refiner, this.QueryBuilder);
                context.Call(dialog, this.ResumeFromRefine);
            }
            else
            {
                await this.Search(context, null);
            }
        }

        protected async Task ResumeFromRefine(IDialogContext context, IAwaitable<string> input)
        {
            await input; // refiner filter is already applied to the SearchQueryBuilder instance we passed in
            await this.Search(context, null);
        }

        protected async Task<GenericSearchResult> ExecuteSearchAsync()
        {
            return await this.SearchClient.SearchAsync(this.QueryBuilder);
        }

        protected abstract string[] GetTopRefiners();

        private async Task ShouldRetry(IDialogContext context, IAwaitable<bool> input)
        {
            try
            {
                bool retry = await input;
                if (retry)
                {
                    await this.PromptRouter(context);
                }
                else
                {
                    context.Done<IList<SearchHit>>(null);
                }
            }
            catch (TooManyAttemptsException)
            {
                context.Done<IList<SearchHit>>(null);
            }
        }

        private async Task ActOnSearchResults(IDialogContext context, IAwaitable<IMessageActivity> input)
        {
            var activity = await input;
            var choice = activity.Text;

            switch (choice.ToLowerInvariant())
            {
                case "again":
                case "reset":
                    this.QueryBuilder.Reset();
                    await this.PromptRouter(context);
                    break;

                case "more":
                    this.QueryBuilder.PageNumber++;
                    await this.Search(context, null);
                    break;

                case "refine":
                    this.SelectRefiner(context);
                    break;

                case "list":
                    await this.ListAddedSoFar(context);
                    context.Wait(this.ActOnSearchResults);
                    break;

                case "done":
                    context.Done(this.selected);
                    break;

                default:
                    await this.AddSelectedItem(context, choice);
                    break;
            }
        }
    }

    public class RootObject
    {
        public string type { get; set; }
        public object data { get; set; }
    }

}
