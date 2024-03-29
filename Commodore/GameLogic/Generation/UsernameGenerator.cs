﻿using System.Collections.Generic;
using System.Linq;
using Commodore.Framework;
using Commodore.Framework.Generators;

namespace Commodore.GameLogic.Generation
{
    public static class UsernameGenerator
    {
        private static readonly char[] _fallbackAlphabet = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly List<string> _usernames = new List<string>();

        public static void Add(string username)
            => _usernames.Add(username);

        public static void AddRange(IEnumerable<string> usernames)
            => _usernames.AddRange(usernames);

        public static string Generate()
        {
            if (_usernames.Any())
                return _usernames[G.Random.Next(0, _usernames.Count - 1)];

            return StringGenerator.GenerateRandomString(_fallbackAlphabet, 6);
        }
    }
}
