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

        public static string TellMeYourId
        {
            get
            {
                return tellMeid[randomizer.Next(0, tellMeid.Length - 1)];
            }
        }
        public static string TellMeYourIdAgain
        {
            get
            {
                return tellMeidAgain[randomizer.Next(0, tellMeidAgain.Length - 1)];
            }
        }
        public static string ERROR_INVALID_USERID
        {
            get
            {
                return "Hmm, sorry I can't use that userid. Please make sure its alphanumeric (a-z, 0-9), with no spaces.";
            }
        }

    }
}
