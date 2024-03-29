﻿using Commodore.EVIL.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace Commodore.EVIL.Abstraction
{
    public class Table : Dictionary<DynValue, DynValue>
    {
        public DynValue this[string key]
        {
            get => GetValueByString(key);

            set
            {
                var dynKey = GetKeyByString(key);

                if (dynKey != null)
                    base[dynKey] = value;
                else
                    Add(new DynValue(key), value);
            }
        }

        public DynValue this[double key]
        {
            get => GetValueByNumber(key);

            set
            {
                var dynKey = GetKeyByNumber(key);

                if (dynKey != null)
                    base[dynKey] = value;
                else
                    Add(new DynValue(key), value);
            }
        }

        public new DynValue this[DynValue key]
        {
            get
            {
                if (key.Type == DynValueType.Number)
                    return this[key.Number];
                else if (key.Type == DynValueType.String)
                    return this[key.String];
                else throw new RuntimeException($"A value type '{key}' cannot be used to index a table.", null);
            }

            set
            {
                if (key.Type == DynValueType.Number)
                    this[key.Number] = value;
                else if (key.Type == DynValueType.String)
                    this[key.String] = value;
                else throw new RuntimeException($"A value type '{key}' cannot be used to index a table.", null);
            }
        }

        public DynValue GetKeyByDynValue(DynValue key)
        {
            switch (key.Type)
            {
                case DynValueType.Number:
                    return GetKeyByNumber(key.Number);
                case DynValueType.String:
                    return GetKeyByString(key.String);
                default: return null;
            }
        }

        public new bool ContainsKey(DynValue key)
            => GetKeyByDynValue(key) != null;

        public DynValue GetKeyByString(string key)
            => Keys.FirstOrDefault(k => k.Type == DynValueType.String && k.String == key);

        public DynValue GetValueByString(string key)
        {
            var dynKey = GetKeyByString(key);

            if (dynKey != null)
                return base[dynKey];

            throw new RuntimeException($"Could not find a value with key of '{key}'.", null);
        }

        public DynValue GetKeyByNumber(double key)
            => Keys.FirstOrDefault(k => k.Type == DynValueType.Number && k.Number == key);

        public DynValue GetValueByNumber(double key)
        {
            var dynKey = GetKeyByNumber(key);

            if (dynKey != null)
                return base[dynKey];

            throw new RuntimeException($"Could not find a value with key of '{key}'.", null);
        }

        public static Table FromString(string s)
        {
            var table = new Table();

            for (var i = 0; i < s.Length; i++)
                table[i] = new DynValue(s[i].ToString());

            return table;
        }
    }
}
