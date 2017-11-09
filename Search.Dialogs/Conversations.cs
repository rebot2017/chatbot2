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
            "Hello! What's your Id ??",
            "Greetings, can you tell me your Id?"
        };

        static readonly String[] tellMeidAgain = {
            "OK, lets try another user id ??",
            "Hmm, so can you use another user id?"
        };

        static readonly String[] sessionChoices = {
            "So what do you want to do?"
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
                return error_prefix + "can't talk to your code server.";
            }
        }
    }
}
