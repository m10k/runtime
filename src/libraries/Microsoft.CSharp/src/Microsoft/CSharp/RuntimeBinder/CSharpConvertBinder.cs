// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Numerics.Hashing;
using Microsoft.CSharp.RuntimeBinder.Semantics;

namespace Microsoft.CSharp.RuntimeBinder
{
    /// <summary>
    /// Represents a dynamic conversion in C#, providing the binding semantics and the details about the operation.
    /// Instances of this class are generated by the C# compiler.
    /// </summary>
    internal sealed class CSharpConvertBinder : ConvertBinder, ICSharpBinder
    {
        [ExcludeFromCodeCoverage]
        public string Name
        {
            get
            {
                Debug.Fail("Name should not be called for this binder");
                return null;
            }
        }

        public BindingFlag BindingFlags => 0;

        public Expr DispatchPayload(RuntimeBinder runtimeBinder, ArgumentObject[] arguments, LocalVariableSymbol[] locals)
        {
            Debug.Assert(arguments.Length == 1);
            return Explicit
                ? runtimeBinder.BindExplicitConversion(arguments, Type, locals)
                : runtimeBinder.BindImplicitConversion(arguments, Type, locals, ConversionKind == CSharpConversionKind.ArrayCreationConversion);
        }

        public void PopulateSymbolTableWithName(Type callingType, ArgumentObject[] arguments)
        {
            // Conversions don't need to do anything, since they're just conversions!
            // After we add payload information, we add conversions for all argument
            // types anyway, so that will get handled there.
        }

        public bool IsBinderThatCanHaveRefReceiver => false;

        CSharpArgumentInfo ICSharpBinder.GetArgumentInfo(int index) => CSharpArgumentInfo.None;

        private CSharpConversionKind ConversionKind { get; }

        private readonly RuntimeBinder _binder;

        private readonly Type _callingContext;

        private bool IsChecked => _binder.IsChecked;

        /// <summary>
        /// Initializes a new instance of the <see cref="CSharpConvertBinder" />.
        /// </summary>
        /// <param name="type">The type to convert to.</param>
        /// <param name="conversionKind">The kind of conversion for this operation.</param>
        /// <param name="isChecked">True if the operation is defined in a checked context; otherwise, false.</param>
        /// <param name="callingContext">The <see cref="Type"/> that indicates where this operation is defined.</param>
        public CSharpConvertBinder(
            Type type,
            CSharpConversionKind conversionKind,
            bool isChecked,
            Type callingContext) :
            base(type, conversionKind == CSharpConversionKind.ExplicitConversion)
        {
            ConversionKind = conversionKind;
            _callingContext = callingContext;
            _binder = new RuntimeBinder(callingContext, isChecked);
        }

        public int BinderEqivalenceHash
        {
            get
            {
                int hash = _callingContext?.GetHashCode() ?? 0;
                hash = HashHelpers.Combine(hash, (int)ConversionKind);
                if (IsChecked)
                {
                    hash = HashHelpers.Combine(hash, 1);
                }

                hash = HashHelpers.Combine(hash, Type.GetHashCode());
                return hash;
            }
        }

        public bool IsEquivalentTo(ICSharpBinder other)
        {
            var otherBinder = other as CSharpConvertBinder;
            if (otherBinder == null)
            {
                return false;
            }

            if (ConversionKind != otherBinder.ConversionKind ||
                IsChecked != otherBinder.IsChecked ||
                _callingContext != otherBinder._callingContext ||
                Type != otherBinder.Type)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Performs the binding of the dynamic convert operation if the target dynamic object cannot bind.
        /// </summary>
        /// <param name="target">The target of the dynamic convert operation.</param>
        /// <param name="errorSuggestion">The binding result to use if binding fails, or null.</param>
        /// <returns>The <see cref="DynamicMetaObject"/> representing the result of the binding.</returns>
        public override DynamicMetaObject FallbackConvert(DynamicMetaObject target, DynamicMetaObject errorSuggestion)
        {
#if ENABLECOMBINDER
            DynamicMetaObject com;
            if (!BinderHelper.IsWindowsRuntimeObject(target) && ComBinder.TryConvert(this, target, out com))
            {
                return com;
            }
#endif
            BinderHelper.ValidateBindArgument(target, nameof(target));
            return BinderHelper.Bind(this, _binder, new[] { target }, null, errorSuggestion);
        }
    }
}
