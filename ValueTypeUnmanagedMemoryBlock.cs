using System;
using System.Runtime.InteropServices;

namespace UnmanagedRefs
{
    internal class ValueTypeUnmanagedMemoryBlock : IUnmanagedMemoryBlock
    {
        /// <summary>
        /// Points to the CLR's target for value types (values are stored at the handle root)
        /// </summary>
        public override IntPtr BridgePointer => Handle;

        /// <summary>
        /// Create a value-type unmanaged memory block that wraps a given pointer for an expected length.
        /// </summary>
        /// <param name="reference">Pointer to wrap</param>
        /// <param name="length">Length of data to wrap</param>
        /// <param name="isWrappingManagedMemory">If the pointer references an object allocated against managed memory or not</param>
        public ValueTypeUnmanagedMemoryBlock(IntPtr reference, int length, bool isWrappingManagedMemory = false) : base(isWrappingManagedMemory)
        {
            Handle = reference;
            BytesAllocated = length;
        }

        /// <summary>
        /// Create a value-type unmanaged memory block of a given size.
        /// </summary>
        /// <param name="bytes"></param>
        public ValueTypeUnmanagedMemoryBlock(int bytes) : base(false) => Allocate(bytes);

        /// <summary>
        /// Securely wipe the contents of the allocated buffer
        /// </summary>
        public override void SecureWipe()
        {
            ZeroFillPointer(Handle, BytesAllocated);
        }

        /// <summary>
        /// Set the value this entire memory block from a given byte array
        /// </summary>
        /// <param name="data">Data to overwrite memory block with</param>
        public override void SetValueFromBytes(byte[] data)
        {
            if (data.Length > BytesAllocated)
                throw new AccessViolationException("Data is too large to fit in allocated space");
            Marshal.Copy(data, 0, BridgePointer, data.Length);
        }

        /// <summary>
        /// Copy this memory block to another memory block
        /// </summary>
        /// <param name="target">Target to copy to</param>
        public override void CopyTo(IUnmanagedMemoryBlock target)
        {
            if (!(target is ValueTypeUnmanagedMemoryBlock))
                throw new NotImplementedException("Must copy to ValueTypeUnmanagedMemoryBlock");
            else if (target.BytesAllocated != this.BytesAllocated)
                throw new NotImplementedException("Must copy between blocks of identical sizes");
            else
                PointerToPointerCopy(Handle, target.Handle, BytesAllocated);
        }

        /// <summary>
        /// Retrieve the memory space associated with stored values (no metadata) for this block
        /// </summary>
        /// <returns>Byte array representing all stored values</returns>
        public override byte[] GetValueSpace() => GetBytesFromHandle(BridgePointer, BytesAllocated);
    }
}
