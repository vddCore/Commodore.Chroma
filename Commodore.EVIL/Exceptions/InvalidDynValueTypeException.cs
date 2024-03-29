﻿using Commodore.EVIL.Abstraction;
using System;

namespace Commodore.EVIL.Exceptions
{
    public class InvalidDynValueTypeException : Exception
    {
        public DynValueType RequestedType { get; }
        public DynValueType ActualType { get; }

        public InvalidDynValueTypeException(string message, DynValueType requestedType, DynValueType actualType) : base(message)
        {
            RequestedType = requestedType;
            ActualType = actualType;
        }
    }
}