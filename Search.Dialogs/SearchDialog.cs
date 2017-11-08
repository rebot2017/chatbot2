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


        public SearchDialog(ISearchClient searchClient, SearchQueryBuilder queryBuilder = null, PromptStyler searchHitStyler = null, bool multipleSelection = false)
        {
            SetField.NotNull(out this.SearchClient, nameof(searchClient), searchClient);

            this.QueryBuilder = queryBuilder ?? new SearchQueryBuilder();
            this.HitStyler = searchHitStyler ?? new SearchHitStyler();
            this.MultipleSelection = multipleSelection;
        }

        public Task StartAsync(IDialogContext context)
        {
            return this.InitialPrompt(context);
        }

        public async Task Search(IDialogContext context, IAwaitable<string> input)
        {
            string text = input != null ? await input : null;
            if (this.MultipleSelection && text != null && text.ToLowerInvariant() == "list")
            {
                await this.ListAddedSoFar(context);
                await this.InitialPrompt(context);
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

        protected virtual Task InitialPrompt(IDialogContext context)
        {
            string prompt = "What's your team name?";

            //if (!this.firstPrompt)
            //{
            //    prompt = "What else would you like to do ?";
            //    //if (this.MultipleSelection)
            //    //{
            //    //    prompt += " You can also *list* all items you've added so far.";
            //    //}
            //}
            PromptDialog.Text(context, this.Init2, prompt);
            //this.firstPrompt = false;
            //var result = new List<string>();
            //string remoteUri1 = "https://raw.githubusercontent.com/zweihan/chat-mvp1/master/chatscripts/gscript.py";
            //string remoteUri4 = "https://raw.githubusercontent.com/zweihan/chat-mvp1/master/chatscripts/wikiscript.py";
            //string remoteUri2 = "https://raw.githubusercontent.com/zweihan/chat-mvp1/master/chatscripts/yfscript.py";
            //string remoteUri3 = "https://raw.githubusercontent.com/zweihan/chat-mvp1/master/chatscripts/bookdeposcript.py";
            //System.Net.WebClient myWebClient = new System.Net.WebClient();
            //myWebClient.DownloadFile(remoteUri1, fullpathGoo);
            //myWebClient.DownloadFile(remoteUri2, fullpathYah);
            //myWebClient.DownloadFile(remoteUri3, fullpathboo);
            //myWebClient.DownloadFile(remoteUri4, fullpathWiki);
            //PromptDialog.Choice<string>(context, this.HandleScrapOrSearch, new List<string>() {"Web Scraping", "Search google for map", "Wiki Search","Book Store search" }, "Please select one of the above options");
            //PromptDialog.Text(context, this.Search, prompt);
            return Task.CompletedTask;
        }

        private async Task Init2(IDialogContext context, IAwaitable<string> result)
        {
            var k = await result;
            if (string.IsNullOrEmpty(k))
            {
                k = "abcde";
            }
            var data = context.UserData;
            data.SetValue("team", k);
            var url = "http://139.59.231.81:5000/api/challenge/" + k + "?params=";
            data.SetValue("url", url);
            PromptDialog.Text(context, this.HandleGenericResponse, "Enter the TICKR for info to be scraped");
        }

        private async Task HandleScrapOrSearch(IDialogContext context, IAwaitable<string> result)
        {
            var k = await result;
            if(k.Equals("Web Scraping"))
            {
                //StateClient stateClient = context.Activity.GetStateClient();
                //BotData userData = await stateClient.BotState.GetUserDataAsync(context.Activity.ChannelId, context.Activity.From.Id);
                //userData.SetProperty<string>("url", "http://128.199.72.227:5000/yfscript?params=");
                //stateClient.BotState.SetUserData(context.Activity.ChannelId, context.Activity.From.Id, userData);
                var data = context.UserData;
                data.SetValue("url", "http://128.199.72.227:5000/yfscript?params=");
                PromptDialog.Text(context, this.HandleGenericResponse, "Enter the TICKR for info to be scraped");
            }
            else if(k.Equals("Search google for map"))
            {
                //StateClient stateClient = context.Activity.GetStateClient();
                //BotData userData = await stateClient.BotState.GetUserDataAsync(context.Activity.ChannelId, context.Activity.From.Id);
                //userData.SetProperty<string>("url", "http://128.199.72.227:5000/gscript?params=");
                //stateClient.BotState.SetUserData(context.Activity.ChannelId, context.Activity.From.Id, userData);
                var data = context.UserData;
                data.SetValue("url", "http://128.199.72.227:5000/gscript?params=");
                PromptDialog.Text(context, this.HandleGenericResponse, "Enter the place to be searched");
            }
            else if(k.Equals("Wiki Search"))
            {
                //StateClient stateClient = context.Activity.GetStateClient();
                //BotData userData = await stateClient.BotState.GetUserDataAsync(context.Activity.ChannelId, context.Activity.From.Id);
                //userData.SetProperty<string>("url", "http://128.199.72.227:5000/wikiscript?params=");
                //stateClient.BotState.SetUserData(context.Activity.ChannelId, context.Activity.From.Id, userData);
                var data = context.UserData;
                data.SetValue("url", "http://128.199.72.227:5000/wikiscript?params=");
                PromptDialog.Text(context, this.HandleGenericResponse, "Enter the wiki text to be searched");
            }
            else if(k.Equals("Book Store search"))
            {
                //StateClient stateClient = context.Activity.GetStateClient();
                //BotData userData = await stateClient.BotState.GetUserDataAsync(context.Activity.ChannelId, context.Activity.From.Id);
                //userData.SetProperty<string>("url", "http://128.199.72.227:5000/bookdeposcript?params=");
                //stateClient.BotState.SetUserData(context.Activity.ChannelId, context.Activity.From.Id, userData);
                var data = context.UserData;
                data.SetValue("url", "http://128.199.72.227:5000/bookdeposcript?params=");
                PromptDialog.Text(context, this.HandleGenericResponse, "Enter the books to be searched");
            }
            //else if (k.Equals("Search for places"))
            //{
            //    PromptDialog.Text(context, this.HandleSeAnalysis, "Let me know the venue you want to search");
            //}
        }

        //private async Task HandleSeAnalysis(IDialogContext context, IAwaitable<string> result)
        //{
        //    var k = await result;
        //    var resultj = File.ReadAllText(@"C:\Users\punganuv\Documents\My Received Files\venues_with_revews(1).json");
        //    var resultp = JsonConvert.DeserializeObject<List<RootObjectS>>(resultj);
        //    var cards = new List<ThumbnailCard>();
        //    foreach (var s in resultp)
        //    {
        //        var card = new ThumbnailCard
        //        {
        //            Title = s.name,
        //            Images = new[] { new CardImage(s.photo_url) },
        //            Text = s.address
        //        };
        //        cards.Add(card);
        //    }
        //    var message = context.MakeMessage();
        //    var resultstr = "Result above";
        //    message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //    message.Attachments = cards.Select(p => p.ToAttachment()).ToList();
        //    context.PostAsync(message);
        //    PromptDialog.Text(context, this.ShouldContinueOps1, resultstr);
        //}

        //private async Task ShouldContinueOps1(IDialogContext context, IAwaitable<string> result)
        //{
        //    var k = await result;
        //    var cards = new List<ThumbnailCard>();
        //    var card1 = new ThumbnailCard
        //    {
        //        Subtitle = "Sentiment analysis",
        //        Images = new[] { new CardImage(new System.Uri(@"C:\temp\pic2.png").AbsoluteUri) },
        //        Buttons = new[] {new CardAction { Title ="Download"} }
        //    };
        //    cards.Add(card1);
        //    var card2 = new ThumbnailCard
        //    {
        //        Subtitle = "Word cloud",
        //        Images = new[] { new CardImage(new System.Uri(@"C:\temp\pic1.png").AbsoluteUri) },
        //        Buttons = new[] { new CardAction { Title = "Download" } }
        //    };
        //    cards.Add(card2);
        //    var message = context.MakeMessage();
        //    var resultstr = "Gregorys Coffee is the best place according the analysis below.";
        //    message.AttachmentLayout = AttachmentLayoutTypes.List;
        //    message.Attachments = cards.Select(p => p.ToAttachment()).ToList();
        //    context.PostAsync(message);
        //    PromptDialog.Text(context, this.ShouldContinueOps, resultstr);
        //}

        private async Task HandleGenericResponse(IDialogContext context, IAwaitable<string> result)
        {
            var k = await result;
            StateClient stateClient = context.Activity.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(context.Activity.ChannelId, context.Activity.From.Id);
            var url = userData.GetProperty<string>("url");
            try
            {
                logger.Info("starting response");
                await RunGenericPyfetch(url, k, context);
            }
            catch (Exception e)
            {
                PromptDialog.Text(context, this.ShouldContinueOps, "Fail whale ... " + e.Message);
            }
        }

        private async Task RunGenericPyfetch(string url1, string args, IDialogContext context)
        {
            string result = string.Empty;
            var parms = args.Replace(" ", "+");
            var url = url1 + parms;
            logger.Info($"{url}");
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AutomaticDecompression = DecompressionMethods.GZip;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    result = reader.ReadToEnd();
                }
                if (!string.IsNullOrEmpty(result))
                {
                    var resultList = JsonConvert.DeserializeObject<List<RootObject>>(result);
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
                    else
                    {
                        PromptDialog.Text(context, this.ShouldContinueOps, "Arghh !! Something wrong in the json data");
                    }
                }
            }
            catch(Exception e)
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


        //private async Task HandleBookSearch(IDialogContext context, IAwaitable<string> result)
        //{
        //    var k = await result;
        //    try
        //    {
        //        logger.Info("starting book search");
        //        run_cmdBookSearch(fullpathboo, k, context);
        //    }
        //    catch (Exception e)
        //    {
        //        await this.InitialPrompt(context);
        //    }
        //}

        //private void run_cmdBookSearch(string cmd, string args, IDialogContext context)
        //{
        //    //string result = string.Empty;
        //    //ProcessStartInfo start = new ProcessStartInfo();
        //    //start.FileName = "python";
        //    //start.Arguments = string.Format("{0} {1}", cmd, args);
        //    //start.UseShellExecute = false;
        //    //start.RedirectStandardOutput = true;
        //    //logger.Info("starting py process");
        //    //using (Process process = Process.Start(start))
        //    //{
        //    //    using (StreamReader reader = process.StandardOutput)
        //    //    {
        //    //        result = reader.ReadToEnd();
        //    //    }
        //    //}
        //    string result = string.Empty;
        //    var parms = args.Replace(" ", "+");
        //    string url = @"http://128.199.72.227:5000/bookdeposcript?params=" + parms;

        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        //    request.AutomaticDecompression = DecompressionMethods.GZip;
        //    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        //    using (Stream stream = response.GetResponseStream())
        //    using (StreamReader reader = new StreamReader(stream))
        //    {
        //        result = reader.ReadToEnd();
        //    }
        //    var cards = new List<ThumbnailCard>();
        //    var resultstr = string.Empty;
        //    logger.Info("py code ret");
        //    if (!string.IsNullOrEmpty(result))
        //    {
        //        var resultFromGoogle = JsonConvert.DeserializeObject<List<RootBookObject>>(result);

        //        if (resultFromGoogle.Any())
        //        {
        //            foreach (var s in resultFromGoogle)
        //            {
        //                var card = new ThumbnailCard
        //                {
        //                    Title = s.description,
        //                    Images = new[] { new CardImage(s.img) },
        //                    Text = s.price
        //                };
        //                cards.Add(card);
        //            }
        //        }
        //        resultstr = "Result above";
        //        var message = context.MakeMessage();
        //        message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //        message.Attachments = cards.Select(p => p.ToAttachment()).ToList();
        //        context.PostAsync(message);
        //    }
        //    else
        //    {
        //        resultstr = "Error in python code";
        //    }

        //    PromptDialog.Text(context, this.ShouldContinueOps, resultstr);
        //}

        //private async Task HandleWikiSearch(IDialogContext context, IAwaitable<string> result)
        //{
        //    var k = await result;
        //    try
        //    {
        //        //run_cmdWikiSearch("C:\\Users\\punganuv\\Downloads\\new4.py",k,context);
        //        logger.Info("wiki search start");
        //        run_cmdWikiSearch(fullpathWiki, k, context);
        //    }
        //    catch (Exception e)
        //    {
        //        await this.InitialPrompt(context);
        //    }
        //}

        //private void run_cmdWikiSearch(string cmd, string args, IDialogContext context)
        //{
        //    //string result = string.Empty;
        //    //ProcessStartInfo start = new ProcessStartInfo();
        //    //start.FileName = "python";
        //    //start.Arguments = string.Format("{0} {1}", cmd, args);
        //    //start.UseShellExecute = false;
        //    //start.RedirectStandardOutput = true;
        //    //logger.Info("py code start");
        //    //using (Process process = Process.Start(start))
        //    //{
        //    //    logger.Info("process code start");
        //    //    using (StreamReader reader = process.StandardOutput)
        //    //    {
        //    //        result = reader.ReadToEnd();
        //    //    }
        //    //}
        //    string result = string.Empty;
        //    var parms = args.Replace(" ", "+");
        //    string url = @"http://128.199.72.227:5000/wikiscript?params=" + parms;

        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        //    request.AutomaticDecompression = DecompressionMethods.GZip;
        //    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        //    using (Stream stream = response.GetResponseStream())
        //    using (StreamReader reader = new StreamReader(stream))
        //    {
        //        result = reader.ReadToEnd();
        //    }
        //    logger.Info("process code end");
        //    //var cards = new List<ThumbnailCard>();
        //    //if (!string.IsNullOrEmpty(result))
        //    //{
        //    //    var resultFromGoogle = JsonConvert.DeserializeObject<WikiObject>(result);

        //    //    if (resultFromGoogle != null)
        //    //    {
        //    //        //foreach (var s in resultFromGoogle)
        //    //        //{
        //    //            var card = new ThumbnailCard
        //    //            {
        //    //                Title = resultFromGoogle.title,
        //    //                //Images = new[] { new CardImage(s.img) },
        //    //                Text = resultFromGoogle.description
        //    //            };
        //    //            cards.Add(card);
        //    //        //}
        //    //    }
        //    //}
        //    //var message = context.MakeMessage();
        //    //message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //    //message.Attachments = cards.Select(p => p.ToAttachment()).ToList();
        //    //context.PostAsync(message);

        //    if (string.IsNullOrEmpty(result))
        //    {
        //        result = "error in python code";
        //    }
        //    PromptDialog.Text(context, this.ShouldContinueOps, result);
        //}

        //private async Task HandleGoogleSearch(IDialogContext context, IAwaitable<string> result)
        //{
        //    var k = await result;
        //    try
        //    {
        //        run_cmdGoogle(fullpathGoo, k, context);
        //    }
        //    catch(Exception e)
        //    {
        //        await this.InitialPrompt(context);
        //    }
        //}

        //private async void run_cmdGoogle(string cmd, string args, IDialogContext context)
        //{
        //    string result = string.Empty;
        //    var parms = args.Replace(" ", "+");
        //    string url = @"http://128.199.72.227:5000/gscript?params=" + parms;

        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        //    request.AutomaticDecompression = DecompressionMethods.GZip;
        //    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        //    using (Stream stream = response.GetResponseStream())
        //    using (StreamReader reader = new StreamReader(stream))
        //    {
        //        result = reader.ReadToEnd();
        //    }

        //    //string result = string.Empty;
        //    //ProcessStartInfo start = new ProcessStartInfo();
        //    //start.FileName = "python";
        //    //start.Arguments = string.Format("{0} {1}", cmd, args);
        //    //start.UseShellExecute = false;
        //    //start.RedirectStandardOutput = true;
        //    //logger.Info("py code start");
        //    //using (Process process = Process.Start(start))
        //    //{
        //    //    logger.Info("process code start");
        //    //    using (StreamReader reader = process.StandardOutput)
        //    //    {
        //    //        result = reader.ReadToEnd();
        //    //    }
        //    //}

        //    logger.Info("process code end");
        //    //HttpClient client = new HttpClient();
        //    //HttpResponseMessage response = await client.GetAsync(result);
        //    var resultstr = string.Empty;
        //    var cards = new List<ThumbnailCard>();
        //    if (!string.IsNullOrEmpty(result))
        //    {
        //        var resultFromGoogle = JsonConvert.DeserializeObject<List<RootGoogleObject>>(result);

        //        if (resultFromGoogle.Any())
        //        {
        //            foreach (var s in resultFromGoogle)
        //            {
        //                var card = new ThumbnailCard
        //                {
        //                    Title = s.title,
        //                    Images = new[] { new CardImage(s.img) },
        //                    Text = s.description
        //                };
        //                cards.Add(card);
        //            }
        //        }
        //        var message = context.MakeMessage();
        //        resultstr = "Result above";
        //        message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //        message.Attachments = cards.Select(p => p.ToAttachment()).ToList();
        //        context.PostAsync(message);
        //    }
        //    else
        //    {
        //        resultstr = "Error in python code";
        //    }
        //    PromptDialog.Text(context, this.ShouldContinueOps, resultstr);
        //    //if (response.IsSuccessStatusCode)
        //    //{
        //    //    string test = await response.Content.ReadAsStringAsync();

        //    //    var resultFromGoogle = JsonConvert.DeserializeObject<RootObject>(test);
        //    //    var cards = new List<ThumbnailCard>();
        //    ////    foreach (var res in resultFromGoogle.results)
        //    ////    {
        //    //        //if (res.photos != null)
        //    //        //{
        //    //            string name = res.name;
        //    //            //string img =res.photos != null ? GetPhoto(res.photos[0].photo_reference): null;
        //    //            string googleLink = "http://maps.google.com/?q=" + res.geometry.location.lat + ',' + res.geometry.location.lng;
        //    //            Uri uri = new Uri(googleLink);
        //    //            var card = new ThumbnailCard
        //    //            {
        //    //                Title = name,
        //    //                Images = new[] { new CardImage(img) },
        //    //                Text = uri.AbsoluteUri
        //    //            };
        //    //            cards.Add(card);
        //    //    //    //}
        //    //    //}

        //    //    var message = context.MakeMessage();
        //    //    message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
        //    //    message.Attachments =  cards.Select(p => p.ToAttachment()).ToList() ;
        //    //    await context.PostAsync(message);
        //    //context.PostAsync(message).Wait();
        //    //}
        //    //RootObject resultFromGoogle = JsonConvert.DeserializeObject<RootObject>(result);
        //    //var result1 = "Displaying options...";
        //    //PromptDialog.Text(context, this.ShouldContinueOps, result1);

        //    //Activity replyToConversation = message.CreateReply("Should go to conversation, in carousel format");
        //    //var cards =  new ThumbnailCard
        //    //{
        //    //    Title = "Test",
        //    //    //Images = new[] { new CardImage(h.PictureUrl) },
        //    //    Text ="Test"
        //    //};


        //    //await context.PostAsync(message);
        //    //var connector =
        //    //new ConnectorClient(new Uri(message.ServiceUrl));
        //    //var reply = await connector.Conversations.SendToConversationAsync(replyToConversation);

        //    //var json = System.json
        //}

        //public string GetPhoto(string photoReference)
        //{
        //    return "https://maps.googleapis.com/maps/api/place/photo?maxwidth=400&photoreference=" + photoReference + "&key=AIzaSyB3YiMlUg_I1xxYcvYRTH65lNdzxSPTTuE";
        //}

        //private async Task HandleTICKRScrap(IDialogContext context, IAwaitable<string> result)
        //{
        //    var k = await result;
        //    run_cmd(fullpathYah,k,context);
        //}

        //private void run_cmd(string cmd, string args,IDialogContext context)
        //{
        //    //string result = string.Empty;
        //    //ProcessStartInfo start = new ProcessStartInfo();
        //    //start.FileName = "python";
        //    //start.Arguments = string.Format("{0} {1}", cmd, args);
        //    //start.UseShellExecute = false;
        //    //start.RedirectStandardOutput = true;
        //    //logger.Info("py code start");
        //    //using (Process process = Process.Start(start))
        //    //{
        //    //    logger.Info("process code start");
        //    //    using (StreamReader reader = process.StandardOutput)
        //    //    {
        //    //        result = reader.ReadToEnd();
        //    //    }
        //    //}
        //    string result = string.Empty;
        //    var parms = args.Replace(" ", "+");
        //    string url = @"http://128.199.72.227:5000/yfscript?params=" + parms;

        //    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        //    request.AutomaticDecompression = DecompressionMethods.GZip;
        //    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        //    using (Stream stream = response.GetResponseStream())
        //    using (StreamReader reader = new StreamReader(stream))
        //    {
        //        result = reader.ReadToEnd();
        //    }
        //    logger.Info("process code end");
        //    if (string.IsNullOrEmpty(result))
        //    {
        //        result = "Error in python code";
        //    }
        //    PromptDialog.Text(context, this.ShouldContinueOps, result);
        //}

        private async Task ShouldContinueOps(IDialogContext context, IAwaitable<string> result)
        {
            await this.InitialPrompt(context);
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
                    PromptDialog.Choice<string>(context, this.ShouldContinueSearching,new List<string>() { "BUY","SELL","KNOW MORE"}, "Please select one of the above options");
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
                    await this.InitialPrompt(context);
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
                    await this.InitialPrompt(context);
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
                    await this.InitialPrompt(context);
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
    //public class Location
    //{
    //    public double lat { get; set; }
    //    public double lng { get; set; }
    //}

    //public class Northeast
    //{
    //    public double lat { get; set; }
    //    public double lng { get; set; }
    //}

    //public class Southwest
    //{
    //    public double lat { get; set; }
    //    public double lng { get; set; }
    //}

    //public class Viewport
    //{
    //    public Northeast northeast { get; set; }
    //    public Southwest southwest { get; set; }
    //}

    //public class Geometry
    //{
    //    public Location location { get; set; }
    //    public Viewport viewport { get; set; }
    //}

    //public class OpeningHours
    //{
    //    public bool open_now { get; set; }
    //    public List<object> weekday_text { get; set; }
    //}

    //public class Photo
    //{
    //    public int height { get; set; }
    //    public List<string> html_attributions { get; set; }
    //    public string photo_reference { get; set; }
    //    public int width { get; set; }
    //}

    //public class Result
    //{
    //    public string formatted_address { get; set; }
    //    public Geometry geometry { get; set; }
    //    public string icon { get; set; }
    //    public string id { get; set; }
    //    public string name { get; set; }
    //    public OpeningHours opening_hours { get; set; }
    //    public List<Photo> photos { get; set; }
    //    public string place_id { get; set; }
    //    public double rating { get; set; }
    //    public string reference { get; set; }
    //    public List<string> types { get; set; }
    //    public int? price_level { get; set; }
    //}

    //public class RootObject
    //{
    //    public List<object> html_attributions { get; set; }
    //    public List<Result> results { get; set; }
    //    public string status { get; set; }
    //}

    public class RootGoogleObject
    {
        public string img { get; set; }
        public string title { get; set; }
        public string description { get; set; }
    }

    public class RootBookObject
    {
        public string img { get; set; }
        public string description { get; set; }
        public string price { get; set; }
    }

    public class WikiObject
    {
        public string title { get; set; }
        public string description { get; set; }
    }

    public class Review
    {
        public string review { get; set; }
        public int no_likes { get; set; }
        public int datetime { get; set; }
    }

    public class RootObjectS
    {
        public string venue_id { get; set; }
        public string name { get; set; }
        public List<Review> reviews { get; set; }
        public string photo_url { get; set; }
        public string address { get; set; }
    }

    public class RootObject
    {
        public string type { get; set; }
        public object data { get; set; }
    }

    public class RootCardObject
    {
        public string img { get; set; }
        public string description { get; set; }
        public string title { get; set; }
    }
}
