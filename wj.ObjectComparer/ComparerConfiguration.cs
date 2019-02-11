﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace wj.ObjectComparer
{
    /// <summary>
    /// Fluent syntax configuration helper to assist in the creation of object comparers.
    /// </summary>
    /// <typeparam name="TSource">The type of the first object to compare.</typeparam>
    /// <typeparam name="TDestination">The type of the second object to compare.</typeparam>
    public class ComparerConfiguration<TSource, TDestination>
    {
        #region Properties
        /// <summary>
        /// Gets the type of the first object to compare.
        /// </summary>
        private Type Type1 { get; }

        /// <summary>
        /// Gets the type of the second object to compare.
        /// </summary>
        private Type Type2 { get; }

        /// <summary>
        /// Gets the type information for the type of the first object to compare.
        /// </summary>
        private TypeInfo TypeInfo1 { get; }

        /// <summary>
        /// Gets the type information for the type of the second object to compare.
        /// </summary>
        private TypeInfo TypeInfo2 { get; }

        /// <summary>
        /// Gets a collection of comparers to add to the object comparer.
        /// </summary>
        private Dictionary<Type, IComparer> Comparers { get; } = new Dictionary<Type, IComparer>();
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new instance of this clas.
        /// </summary>
        /// <param name="ignoreAttributedPropertyMappings">Boolean value that controls if type 
        /// scanning should include property maps defined via the <see cref="PropertyMapAttribute"/> 
        /// attribute.  The default value is false, meaning that property maps will not be ignored.</param>
        internal ComparerConfiguration(bool ignoreAttributedPropertyMappings = false)
        {
            Type1 = typeof(TSource);
            Type2 = typeof(TDestination);
            TypeInfo1 = GetTypeInformation(Type1, ignoreAttributedPropertyMappings);
            TypeInfo2 = GetTypeInformation(Type2, ignoreAttributedPropertyMappings);
        }
        #endregion

        #region Methods
        /// <summary>
        /// Obtains type information from the scanner cache if possible, or scans the data type 
        /// if the scanner cache does not have it.  If the latter, note that the scanner cache is 
        /// not updated.
        /// </summary>
        /// <param name="type">The type whose type information is needed.</param>
        /// <param name="ignoreAttributedPropertyMappings">Boolean value that controls if type 
        /// scanning should include property maps defined via the <see cref="PropertyMapAttribute"/> 
        /// attribute.  The default value is false, meaning that property maps will not be ignored.</param>
        /// <returns>The type information for the passed data type.</returns>
        private TypeInfo GetTypeInformation(Type type, bool ignoreAttributedPropertyMappings)
        {
            bool exists = Scanner.TryGetTypeInformation(type, out TypeInfo ti);
            if (!exists || (ti.PropertyMapsIgnored != ignoreAttributedPropertyMappings && !ignoreAttributedPropertyMappings))
            {
                ti = Scanner.BuildTypeInformation(type, ignoreAttributedPropertyMappings);
            }
            else if (exists)
            {
                ti = ti.Clone(!ignoreAttributedPropertyMappings);
            }
            return ti;
        }

        /// <summary>
        /// Gets the property information object for the property in the given lambda expression.
        /// </summary>
        /// <typeparam name="TObject">The type of object that provides the property in the 
        /// expression.</typeparam>
        /// <typeparam name="TProperty">The type of the property in the expression.</typeparam>
        /// <param name="expr">Lambda expression that defines the property of interest.</param>
        /// <param name="paramName">Parameter name used when throwing exceptions.</param>
        /// <returns>The property information object of the property specified in the lambda 
        /// expression.</returns>
        /// <exception cref="ArgumentException">Thrown if the lambda expression does not conform 
        /// to an expression of a property of the <typeparamref name="TObject"/> data type.</exception>
        private PropertyInfo GetPropertyInfo<TObject, TProperty>(Expression<Func<TObject, TProperty>> expr, string paramName)
        {
            MemberExpression member = expr.Body as MemberExpression;
            Guard.ArgumentCondition(() => member != null, paramName, $"Expression '{expr}' refers to a method, not a property.");
            PropertyInfo propInfo = member.Member as PropertyInfo;
            Guard.ArgumentCondition(() => propInfo != null, paramName, $"Expression '{expr}' refers to a field, not a property.");
            Type type = typeof(TObject);
            Guard.ArgumentCondition(
                () => type == propInfo.ReflectedType || type.IsSubclassOf(propInfo.ReflectedType),
                paramName,
                $"Expression '{expr}' refers to a property that is not from type {type}."
            );
            return propInfo;
        }

        /// <summary>
        /// Maps a property in a target type to a property in the source type for comparison 
        /// purposes.
        /// </summary>
        /// <typeparam name="TSourceProperty">The type of the source property.</typeparam>
        /// <typeparam name="TTargetProperty">The type of the target property.</typeparam>
        /// <param name="sourcePropExpr">Source property lambda expression.</param>
        /// <param name="targetPropExpr">Target property lambda expression.</param>
        /// <param name="forceStringValue">Optional Boolean value that determines if the 
        /// comparison routine should coerce both source and target values to a string before 
        /// performing the comparison.</param>
        /// <param name="formatString">Format string to use during string coercion of the source 
        /// property value.</param>
        /// <param name="targetFormatString">Format string to use during string coercion of the 
        /// target property value.</param>
        /// <returns>This configuration object to enable fluent syntax.</returns>
        public ComparerConfiguration<TSource, TDestination> MapProperty<TSourceProperty, TTargetProperty>(
            Expression<Func<TSource, TSourceProperty>> sourcePropExpr,
            Expression<Func<TDestination, TTargetProperty>> targetPropExpr,
            bool forceStringValue = false,
            string formatString = null,
            string targetFormatString = null
        )
        {
            PropertyInfo piSource = GetPropertyInfo(sourcePropExpr, nameof(sourcePropExpr));
            PropertyInfo piTarget = GetPropertyInfo(targetPropExpr, nameof(targetPropExpr));
            PropertyMap mapping = new PropertyMap(
                Type2,
                PropertyMapOperation.MapToProperty,
                piTarget.Name,
                forceStringValue,
                formatString,
                targetFormatString
            );
            TypeInfo1.Properties[piSource.Name].Maps.Replace(mapping);
            return this;
        }

        /// <summary>
        /// Configures the property comparison information of the source property to ignore the 
        /// specified property during the comparison routine against objects of type 
        /// <see cref="TDestination"/>.
        /// </summary>
        /// <typeparam name="TSourceProperty">The type of the source property.</typeparam>
        /// <param name="sourcePropExpr">Source property lambda expression.</param>
        /// <returns>This configuration object to enable fluent syntax.</returns>
        public ComparerConfiguration<TSource, TDestination> IgnoreProperty<TSourceProperty>(
            Expression<Func<TSource, TSourceProperty>> sourcePropExpr
        )
        {
            PropertyInfo piSource = GetPropertyInfo(sourcePropExpr, nameof(sourcePropExpr));
            PropertyComparisonInfo pci = TypeInfo1.Properties[piSource.Name];
            //Ignore only for the specified data type.
            PropertyMap map = new PropertyMap(
                Type2,
                PropertyMapOperation.IgnoreProperty
            );
            pci.Maps.Replace(map);
            return this;
        }

        /// <summary>
        /// Adds a comparer of a specific type to the configuration object that will be passed on 
        /// to any object comparers created using this configuration object.
        /// </summary>
        /// <typeparam name="TProperty">The property type the comparer can compare.</typeparam>
        /// <param name="comparer">Comparer object for the specified property type.</param>
        /// <returns>This configuration object to enable fluent syntax.</returns>
        public ComparerConfiguration<TSource, TDestination> AddComparer<TProperty>(IComparer comparer)
        {
            Comparers[typeof(TProperty)] = comparer;
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="ObjectComparer"/> object with the configured types and 
        /// settings.
        /// </summary>
        /// <returns>A newly created object comparer object.</returns>
        public ObjectComparer CreateComparer()
        {
            return new ObjectComparer(Type1, Type2, TypeInfo1, TypeInfo2, Comparers);
        }
        #endregion
    }
}
