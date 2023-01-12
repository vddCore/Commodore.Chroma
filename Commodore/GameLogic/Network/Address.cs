using System;
using System.Text;
using System.Text.RegularExpressions;
using Commodore.GameLogic.Persistence;

namespace Commodore.GameLogic.Network
{
    [Serializable]
    public struct Address
    {
        [NonSerialized]
        private static readonly Regex _addrValidator =
            new Regex(@"(?<CorePartA>[A-Fa-f0-9]{4})\:(?<CorePartB>[A-Fa-f0-9]{4})(\+(?<Service>[A-Fa-f0-9]{2}))?");

        public uint Core { get; }
        public byte Service { get; }

        public Address(uint core)
            : this(core, 0)
        {
        }

        public Address(uint core, byte service)
        {
            Core = core;
            Service = service;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append(((Core & 0xFFFF0000) >> 16).ToString("X4"));
            sb.Append(':');
            sb.Append(((Core & 0x0000FFFF)).ToString("X4"));
            sb.Append('+');
            sb.Append(Service.ToString("X2"));

            return sb.ToString();
        }

        public static Address Random()
            => new Address((uint)UserProfile.Instance.Random.Next());

        public static Address Parse(string address)
        {
            uint core = 0;
            byte service = 0;

            var match = _addrValidator.Match(address);

            if (!match.Success)
                throw new FormatException("Invalid network address provided.");

            var corePartA = Convert.ToUInt16(match.Groups["CorePartA"].Value, 16);
            var corePartB = Convert.ToUInt16(match.Groups["CorePartB"].Value, 16);

            core |= (uint)(corePartA << 16);
            core |= corePartB;

            if (!string.IsNullOrWhiteSpace(match.Groups["Service"].Value))
                service = Convert.ToByte(match.Groups["Service"].Value, 16);

            return new Address(core, service);
        }

        public override int GetHashCode()
            => Core.GetHashCode();
    }
}