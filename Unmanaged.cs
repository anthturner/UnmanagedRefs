using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace UnmanagedRefs
{
    public abstract class Unmanaged
    {
        protected static List<WeakReference> _refs = new List<WeakReference>();

        /// <summary>
        /// Type being wrapped by the inheriting generic
        /// </summary>
        internal abstract Type WrappedT { get; }

        /// <summary>
        /// Clear immutable value (set to default(T))
        /// </summary>
        internal abstract void SetImmutableValue();

        /// <summary>
        /// Set immutable value to value
        /// </summary>
        /// <param name="value">Value to set in immutable container</param>
        internal abstract void SetImmutableValue(object value);

        /// <summary>
        /// Reference to memory block being wrapped by this instance
        /// </summary>
        internal IUnmanagedMemoryBlock MemoryBlock { get; set; }

        /// <summary>
        /// Allocate a new unmanaged memory block of a sufficient size to store type T
        /// </summary>
        protected Unmanaged()
        {
            MemoryBlock = GetMemoryBlock();
            _refs.Add(new WeakReference(this, false));
        }

        /// <summary>
        /// Allocate a new unmanaged memory block of a given size
        /// </summary>
        /// <param name="valueAllocationSize">Size of value block to allocate (not including metadata if necessary)</param>
        protected Unmanaged(int valueAllocationSize)
        {
            MemoryBlock = GetMemoryBlock(valueAllocationSize);
            _refs.Add(new WeakReference(this, false));
        }

        private IUnmanagedMemoryBlock GetMemoryBlock()
        {
            if (NeedsReferenceType())
                return new ReferenceTypeUnmanagedMemoryBlock(GetAllocationRequirements());
            else
                return new ValueTypeUnmanagedMemoryBlock(GetAllocationRequirements());
        }
        private IUnmanagedMemoryBlock GetMemoryBlock(int valueSize)
        {
            if (NeedsReferenceType())
                return new ReferenceTypeUnmanagedMemoryBlock(valueSize + 8);
            else
                return new ValueTypeUnmanagedMemoryBlock(valueSize);
        }
        internal IUnmanagedMemoryBlock GetMemoryBlock(object target)
        {
            if (NeedsReferenceType())
            {
                var refPtr = target.GetNativeClrPointer();
                return new ReferenceTypeUnmanagedMemoryBlock(refPtr, GetAllocationRequirements(refPtr));
            }
            else
            {
                var valueBytes = new byte[Marshal.SizeOf(WrappedT)];
                var valueHandle = GCHandle.Alloc(valueBytes, GCHandleType.Pinned);
                Marshal.StructureToPtr(target, valueHandle.AddrOfPinnedObject(), false);
                valueHandle.Free();

                var block = new ValueTypeUnmanagedMemoryBlock(GetAllocationRequirements());
                Marshal.Copy(valueBytes, 0, block.BridgePointer, valueBytes.Length);

                GC.KeepAlive(target);

                return block;
            }
        }
        internal IUnmanagedMemoryBlock GetMemoryBlock(IntPtr target, bool isWrappingManagedMemory = false)
        {
            if (NeedsReferenceType())
                return new ReferenceTypeUnmanagedMemoryBlock(target, GetAllocationRequirements(target), isWrappingManagedMemory);
            else
                return new ValueTypeUnmanagedMemoryBlock(target, GetAllocationRequirements(target), isWrappingManagedMemory);
        }


        private int GetAllocationRequirements() => GetAllocationRequirements(WrappedT);
        private static int GetAllocationRequirements(Type t)
        {
            if (t.IsValueType)
                return Marshal.SizeOf(t);
            else if (t.IsArray)
            {
                if (t.GetElementType().IsValueType)
                    return 12; // 12b overhead for arrays with value types (0 elements)
                else
                    return 16; // 16b overhead for arrays with ref types (0 elements)
            }
            else if (t == typeof(string))
                return 12; // 12b overhead for strings
            else
                return Marshal.ReadInt32(t.TypeHandle.Value, 4);
        }
        private int GetAllocationRequirements(IntPtr target) => GetAllocationRequirements(WrappedT, target);
        private static int GetAllocationRequirements(Type t, IntPtr target)
        {
            if (t.IsValueType)
                return Marshal.SizeOf(t);
            else if (t.IsArray || t == typeof(string))
            {
                var length = Marshal.ReadInt32(target + 4);

                int arrayTypeLen = 0;
                if (t == typeof(string))
                    arrayTypeLen = 2; // 2b per char
                else
                    arrayTypeLen = GetAllocationRequirements(t.GetElementType());

                if (t == typeof(string) || t.GetElementType().IsValueType)
                    return 12 + length * arrayTypeLen; // 12b overhead + len * size_of_array_type
                else
                    return 16 + length * arrayTypeLen; // 16b overhead + len * size_of_array_type
            }
            else
                return Marshal.ReadInt32(t.TypeHandle.Value, 4);
        }

        internal bool NeedsReferenceType() => NeedsReferenceType(WrappedT);
        private static bool NeedsReferenceType(Type t)
        {
            if (t.IsArray || t == typeof(string))
                return true;
            if (t.IsValueType)
                return false;
            return true;
        }

        /// <summary>
        /// Export the value of this unmanaged memory block
        /// </summary>
        /// <returns>Exported value of this block</returns>
        public byte[] Export() => MemoryBlock.GetValueSpace();

        /// <summary>
        /// Create a new unmanaged memory block to represent a given exported value
        /// </summary>
        /// <typeparam name="T">Runtime type of value being wrapped</typeparam>
        /// <param name="bytes">Bytes to import as the value for T</param>
        /// <returns>Unmanaged strongly-typed pointer to new block</returns>
        public static Unmanaged<T> CreateFromExport<T>(byte[] bytes)
        {
            var wrapper = new Unmanaged<T>(bytes.Length);
            wrapper.MemoryBlock.SetValueFromBytes(bytes);
            var refBlock = wrapper.MemoryBlock as ReferenceTypeUnmanagedMemoryBlock;
            if (refBlock != null)
                refBlock.SetRuntimeTypeHandle(typeof(T));

            wrapper.SetImmutableValue(wrapper.Get());

            return wrapper;
        }
    }

    public class Unmanaged<T> : Unmanaged, IDisposable
    {
        internal override Type WrappedT => typeof(T);

        internal class ImmutableValueContainer
        {
            public readonly T Value;
            public ImmutableValueContainer() => Value = default(T);
            public ImmutableValueContainer(T val) => Value = val;
        }

        public static implicit operator T(Unmanaged<T> @ref) => @ref.ImmutableValue.Value;
        public static implicit operator Unmanaged<T>(T obj) => typeof(T).IsValueType ? new Unmanaged<T>(obj) : new Unmanaged<T>(obj.GetNativeClrPointer());

        public override string ToString() => ImmutableValue.Value.ToString();
        public override int GetHashCode() => ImmutableValue.Value.GetHashCode();
        public override bool Equals(object obj) => ImmutableValue.Value.Equals(obj);

        /* Developer's Note: 
         * The below container is necessary to encapsulate the value (T) field, which *must* be set to 
         *   readonly for the pointer magic to actually work against the delegated reference.
         */
        private ImmutableValueContainer ImmutableValue { get; set; }

        /// <summary>
        /// Strongly-typed value of this unmanaged memory instance
        /// </summary>
        public T Value => ImmutableValue.Value;

        internal override void SetImmutableValue() => ImmutableValue = new ImmutableValueContainer();
        internal override void SetImmutableValue(object value) => ImmutableValue = new ImmutableValueContainer((T)value);

        /// <summary>
        /// Create a new allocation of unmanaged memory sufficient to fit an instance of T
        /// </summary>
        public Unmanaged() : base()
        {
            SetImmutableValue();
        }

        /// <summary>
        /// Create a new allocation of unmanaged memory from an existing (managed/unmanaged) variable instance
        /// </summary>
        /// <param name="value">Instance of T to wrap</param>
        public Unmanaged(T value) : base()
        {
            Set(value);
        }

        /// <summary>
        /// Create a new allocation of unmanaged memory of a given size
        /// </summary>
        /// <param name="bytes">Size of memory to allocate</param>
        internal Unmanaged(int bytes) : base(bytes)
        {
            SetImmutableValue();
        }

        /// <summary>
        /// Create a new allocation of unmanaged memory to wrap a given IntPtr, representing an Unmanaged instance
        /// </summary>
        /// <param name="bridgePointer"></param>
        public Unmanaged(IntPtr bridgePointer)
        {
            // assume wrapping managed memory
            MemoryBlock = GetMemoryBlock(bridgePointer, true);

            _refs.RemoveAll(r => !r.IsAlive);
            if (_refs.Any(r => r.Target.GetType() == GetType() && bridgePointer.Equals(((Unmanaged<T>)r.Target).MemoryBlock.BridgePointer)))
                MemoryBlock = GetMemoryBlock(bridgePointer); // this IntPtr is actually an Unmanaged<T> object, shift the block pointer and wrap it

            ImmutableValue = new ImmutableValueContainer(Get());
        }

        /// <summary>
        /// Set the value of unmanaged memory to an existing value
        /// </summary>
        /// <param name="value">Value to set</param>
        public void Set(T value)
        {
            IUnmanagedMemoryBlock incomingValueBlock;
            if (NeedsReferenceType())
                incomingValueBlock = GetMemoryBlock(value.GetNativeClrPointer());
            else
                incomingValueBlock = GetMemoryBlock(value);

            incomingValueBlock.CopyTo(MemoryBlock);
            ImmutableValue = new ImmutableValueContainer(Get());
        }

        /// <summary>
        /// Retrieve the strongly-typed value of the unmanaged memory block
        /// </summary>
        /// <returns>Strongly-typed value of unmanaged memory block</returns>
        public T Get()
        {
            if (typeof(T).IsValueType)
            {
                var dataBytes = MemoryBlock.GetEntireSpace();
                var dataHandle = GCHandle.Alloc(dataBytes, GCHandleType.Pinned);
                T dataStructure = (T)Marshal.PtrToStructure(dataHandle.AddrOfPinnedObject(), typeof(T));
                dataHandle.Free();
                return dataStructure;
            }
            else
                return MemoryBlock.BridgePointer.GetNativeClrObject<T>();
        }      

        /// <summary>
        /// Wipe and release allocated memory
        /// </summary>
        public void Dispose()
        {
            if (MemoryBlock != null)
            {
                MemoryBlock.SecureWipe();
                MemoryBlock.Release();
            }
        }
    }
}
