using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.Dialogs
{
    public static class Conversations
    {
        static readonly Random randomizer = new Random();

        static readonly String[] tellMeid = {
            "Hello! What's your userid? (Try your ntid)",
            "Greetings, can you tell me your Id? (Try your ntid)"
        };

        static readonly String[] tellMeidAgain = {
            "OK, so let's try another user id? (Use your ntid)",
            "Hmm, can you use another user id? (Try your ntid)"
        };

        static readonly String[] sessionChoices = {
            "What would you like to do?"
        };

        public static string TellMeYourId
        {
            get
            {
                return randomString(tellMeid);
            }
        }

        public static string TellMeYourIdAgain
        {
            get
            {
                return randomString(tellMeidAgain);
            }
        }

        public static string ChooseYourSession
        {
            get
            {
                return randomString(sessionChoices);
            }
        }

        public static string PleaseEnterTickerInformation
        {
            get
            {
                return "Please enter a stock TICKR (eg. MSFT, AAPL, GOOG)";
            }
        }

        public static string PleaseEnterPracticeRequest
        {
            get
            {
                return "Try asking something";
            }
        }

        static string randomString(string[] stringSet)
        {
            return stringSet[randomizer.Next(0, stringSet.Length - 1)];
        }

        public static string ERROR_INVALID_USERID
        {
            get
            {
                return "Hmm, sorry I can't use that userid. Please make sure its alphanumeric (a-z, 0-9), with no spaces.";
            }
        }

        static string error_prefix
        {
            get
            {
                return "Uh oops.. Seems like I ";
            }
        }

        public static string ERROR_CANNOT_CONNECT_TO_PYTHONAPI
        {
            get
            {
                return error_prefix + "can't talk to your code server. Please try again.";
            }
        }
    }
}
