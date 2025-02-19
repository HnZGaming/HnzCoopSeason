using System;

namespace HnzPveSeason
{
    public sealed class MerchantTypePicker
    {
        readonly Random _random;
        readonly MerchantType[] _types;

        public MerchantTypePicker(int seed)
        {
            _random = new Random(seed);
            _types = (MerchantType[])Enum.GetValues(typeof(MerchantTypePicker));
        }

        public MerchantType Next()
        {
            return _types[_random.Next(_types.Length)];
        }
    }
}