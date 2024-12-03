using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace NetBuff.Relays
{
    [Serializable]
    [BurstCompatible]
    public struct DataBuffer
    {
        public short length;
        [SerializeField]
        private FixedList4096Bytes<byte> list0;
        [SerializeField]
        private FixedList4096Bytes<byte> list1;
        [SerializeField]
        private FixedList4096Bytes<byte> list2;
        
        public DataBuffer(NativeArray<byte> data) : this()
        {
            length = (short) data.Length;

            unsafe
            {
                list0 = new FixedList4096Bytes<byte>();
                if (data.Length > 0) list0.AddRange(data.GetUnsafePtr(), _Min(data.Length, 4094));
                
                list1 = new FixedList4096Bytes<byte>();
                if (data.Length > 4094) list1.AddRange(_Offset(data.GetUnsafePtr(), 4094), _Min(data.Length - 4094, 4094));
                
                list2 = new FixedList4096Bytes<byte>();
                if (data.Length > 8192) list2.AddRange(_Offset(data.GetUnsafePtr(), 8192), _Min(data.Length - 8192, 4094));
            }
        }
        
        public byte[] ToArray()
        {
            var array = new byte[length];

            if (length > 0)
            {
                var array1 = list0.ToArray();
                Array.Copy(array1, 0, array, 0, array1.Length);
            }
            
            if (length > 4094)
            {
                var array2 = list1.ToArray();
                Array.Copy(array2, 0, array, 4094, array2.Length);
            }
            
            if (length > 8192)
            {
                var array3 = list2.ToArray();
                Array.Copy(array3, 0, array, 8192, array3.Length);
            }

            return array;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int _Min(int a, int b)
        {
            return a < b ? a : b;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void* _Offset(void* ptr, int offset)
        {
            return (void*) (((IntPtr) ptr) + offset);
        }
    }
}