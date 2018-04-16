using System;
using System.Runtime.InteropServices;

namespace UnmanagedRefs
{
    internal class ReferenceTypeUnmanagedMemoryBlock : IUnmanagedMemoryBlock
    {
        /// <summary>
        /// Pointer to the location containing values
        /// </summary>
        public IntPtr ValuePtr => Handle + 8;

        /// <summary>
        /// Pointer to the location containing the pointer to the RuntimeTypeHandle
        /// </summary>
        public IntPtr RuntimeTypeHandlePtr => Handle + 4;

        /// <summary>
        /// Pointer to the sync block
        /// </summary>
        public IntPtr SyncPtr => Handle;

        public IntPtr RuntimeTypeHandle => Marshal.ReadIntPtr(RuntimeTypeHandlePtr);

        public override IntPtr BridgePointer => RuntimeTypeHandlePtr;

        public ReferenceTypeUnmanagedMemoryBlock(IntPtr reference, int length, bool isWrappingManagedMemory = false) : base(isWrappingManagedMemory)
        {
            Handle = reference - 4;
            BytesAllocated = length;
        }

        public ReferenceTypeUnmanagedMemoryBlock(int bytes) : base(false) => Allocate(bytes);

        public override void SecureWipe()
        {
            ZeroFillPointer(ValuePtr, BytesAllocated - 8);
        }

        public void SetRuntimeTypeHandle(Type t)
        {
            Marshal.WriteIntPtr(RuntimeTypeHandlePtr, t.TypeHandle.Value);
        }

        public override void SetValueFromBytes(byte[] data)
        {
            if (data.Length > BytesAllocated - 8)
                Allocate(data.Length + 8);
            Marshal.Copy(data, 0, ValuePtr, data.Length);
        }

        public override void CopyTo(IUnmanagedMemoryBlock target)
        {
            if (!(target is ReferenceTypeUnmanagedMemoryBlock))
                throw new NotImplementedException("Must copy to ReferenceTypeUnmanagedMemoryBlock");

            var refTarget = target as ReferenceTypeUnmanagedMemoryBlock;
            refTarget.Allocate(BytesAllocated);
            PointerToPointerCopy(BridgePointer, target.BridgePointer, BytesAllocated-4);
        }

        public void CopyEntireSpace(IUnmanagedMemoryBlock target)
        {
            if (!(target is ReferenceTypeUnmanagedMemoryBlock))
                throw new NotImplementedException("Must copy to ReferenceTypeUnmanagedMemoryBlock");

            var refTarget = target as ReferenceTypeUnmanagedMemoryBlock;
            refTarget.Allocate(BytesAllocated);
            PointerToPointerCopy(Handle, target.Handle, BytesAllocated);
        }

        public override byte[] GetValueSpace() => GetBytesFromHandle(ValuePtr, BytesAllocated - 8);
    }
}
