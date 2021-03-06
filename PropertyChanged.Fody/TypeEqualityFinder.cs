﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

public partial class ModuleWeaver
{
<<<<<<< HEAD
    Dictionary<TypeReference, EqualityComparerRef> equalityComparerCache = new Dictionary<TypeReference, EqualityComparerRef>();
=======
    Dictionary<string, MethodReference> methodCache;
    public int OrdinalStringComparison;
>>>>>>> pr/1

    public EqualityComparerRef GetEqualityComparer(TypeReference targetType)
    {
<<<<<<< HEAD
        EqualityComparerRef result;
        if (!equalityComparerCache.TryGetValue(targetType, out result))
=======
        methodCache = new Dictionary<string, MethodReference>();

        OrdinalStringComparison = (int) StringEquals
            .Parameters[2]
            .ParameterType
            .Resolve()
            .Fields
            .First(x => x.Name == "Ordinal")
            .Constant;

        NotifyNodes.ForEach(FindComparisonMethods);

        methodCache = null;
    }

    void FindComparisonMethods(TypeNode node)
    {
        foreach (var data in node.PropertyDatas)
        {
            data.EqualsMethod = FindTypeEquality(data);
        }

        node.Nodes.ForEach(FindComparisonMethods);
    }

    MethodReference FindTypeEquality(PropertyData propertyData)
    {
        var typeDefinition = propertyData.PropertyDefinition.PropertyType;
        var fullName = typeDefinition.FullName;
        if (methodCache.TryGetValue(fullName, out var methodReference))
>>>>>>> pr/1
        {
            var equalityComparerType = new GenericInstanceType(ModuleDefinition.ImportReference(typeof(EqualityComparer<>)));
            equalityComparerType.GenericArguments.Add(ModuleDefinition.ImportReference(targetType));

<<<<<<< HEAD
            var equalityComparerRef = ModuleDefinition.ImportReference(equalityComparerType);
            var equalityComparerDef = equalityComparerRef.Resolve();

            var defaultMethodDef = equalityComparerDef.Methods.First(m => m.Name == "get_Default");
            var equalsMethodDef = FindNamedMethod(equalityComparerDef, "Equals", equalityComparerDef.GenericParameters[0]);

            var defaultMethodRef = ModuleDefinition.ImportReference(defaultMethodDef);
            var equalsMethodRef = ModuleDefinition.ImportReference(equalsMethodDef);

            defaultMethodRef.DeclaringType = equalityComparerRef;
            equalsMethodRef.DeclaringType = equalityComparerRef;

            equalityComparerCache.Add(targetType, result = new EqualityComparerRef(defaultMethodRef, equalsMethodRef));
        }

        return result;
=======
        var equality = GetEquality(typeDefinition);
        methodCache[fullName] = equality;
        return equality;
    }

    MethodReference GetEquality(TypeReference typeDefinition)
    {
        if (typeDefinition.IsArray)
        {
            return null;
        }
        if (typeDefinition.IsGenericParameter)
        {
            return null;
        }
        if (typeDefinition.Namespace.StartsWith("System.Collections"))
        {
            return null;
        }
        if (typeDefinition.IsGenericInstance)
        {
            if (typeDefinition.FullName.StartsWith("System.Nullable"))
            {
                var genericInstanceMethod = new GenericInstanceMethod(NullableEqualsMethod);
                var typeWrappedByNullable = ((GenericInstanceType) typeDefinition).GenericArguments.First();
                genericInstanceMethod.GenericArguments.Add(typeWrappedByNullable);

                if (typeWrappedByNullable.IsGenericParameter)
                {
                    return ModuleDefinition.ImportReference(genericInstanceMethod, typeWrappedByNullable.DeclaringType);
                }
                return ModuleDefinition.ImportReference(genericInstanceMethod);
            }
        }

        return GetStaticEquality(typeDefinition);
    }

    MethodReference GetStaticEquality(TypeReference typeReference)
    {
        TypeDefinition typeDefinition;
        try
        {
            typeDefinition = Resolve(typeReference);
        }
        catch (Exception ex)
        {
            LogWarning($"Ignoring static equality of type {typeReference.FullName} => {ex.Message}");
            return null;
        }

        if (typeDefinition.IsInterface)
        {
            return null;
        }

        MethodReference equality = null;
        var typesChecked = new List<string>();

        if (UseStaticEqualsFromBase)
        {
            while (equality == null &&
                   typeReference != null &&
                   typeReference.FullName != typeof(object).FullName &&
                   !methodCache.TryGetValue(typeReference.FullName, out equality))
            {
                typesChecked.Add(typeReference.FullName);
                equality = FindNamedMethod(typeReference);
                if (equality == null)
                    typeReference = GetBaseType(typeReference);
            }
        }
        else
        {
            equality = FindNamedMethod(typeReference);
        }

        if (equality != null)
        {
            equality = ModuleDefinition.ImportReference(equality);
        }

        typesChecked.ForEach(typeName => methodCache[typeName] = equality);

        return equality;
    }

    TypeReference GetBaseType(TypeReference typeReference)
    {
        var typeDef = typeReference as TypeDefinition ?? typeReference.Resolve();
        var baseType = typeDef?.BaseType;

        if (baseType == null)
        {
            return null;
        }

        if (baseType.IsGenericInstance && typeReference.IsGenericInstance)
        {
            //currently we have something like: baseType = BaseClass<T>, typeReference = Class<int> (where the class inherits from BaseClass<T> and int is the parameter for T).
            //We want BaseClass<int> -> map generic arguments to the actual parameter types
            var genericBaseType = (GenericInstanceType)baseType;
            var genericTypeRef = (GenericInstanceType)typeReference;

            //create a map from the type reference (child class): generic argument name -> type
            var typeRefDict = new Dictionary<string, TypeReference>();
            var typeRefParams = genericTypeRef.ElementType.Resolve().GenericParameters;
            for (var i = 0; i < typeRefParams.Count; i++)
            {
                var paramName = typeRefParams[i].FullName;
                var paramType = genericTypeRef.GenericArguments[i];
                typeRefDict[paramName] = paramType;
            }

            //apply to base type
            //note: even though the base class may have different argument names in the source code, the argument names of the inheriting class are used in the GenericArguments
            //thus we can directly map them.
            var baseTypeArgs = genericBaseType.GenericArguments.Select(arg =>
            {
                if (typeRefDict.TryGetValue(arg.FullName, out var t))
                {
                    return t;
                }

                return arg;
            }).ToArray();

            baseType = genericBaseType.ElementType.MakeGenericInstanceType(baseTypeArgs);
        }

        return baseType;
    }

    public static MethodReference FindNamedMethod(TypeReference typeReference)
    {
        var typeDefinition = typeReference.Resolve();
        var equalsMethod = FindNamedMethod(typeDefinition, "Equals", typeReference);
        if (equalsMethod == null)
        {
            equalsMethod = FindNamedMethod(typeDefinition, "op_Equality", typeReference);
        }
        if (equalsMethod != null && typeReference.IsGenericInstance)
        {
            var genericType = new GenericInstanceType(equalsMethod.DeclaringType);
            foreach (var argument in ((GenericInstanceType) typeReference).GenericArguments)
            {
                genericType.GenericArguments.Add(argument);
            }
            equalsMethod = MakeGeneric(genericType, equalsMethod);
        }
        return equalsMethod;
>>>>>>> pr/1
    }

    static MethodReference FindNamedMethod(TypeDefinition typeDefinition, string methodName, TypeReference parameterType)
    {
<<<<<<< HEAD
        return typeDefinition.Methods
            .First(x =>
                x.Name == methodName &&
                !x.IsStatic &&
                x.ReturnType.Name == "Boolean" &&
                x.HasParameters &&
                x.Parameters.Count == 2 &&
                MatchParameter(x.Parameters[0], parameterType) &&
                MatchParameter(x.Parameters[1], parameterType)
            );
=======
        MethodReference reference =  typeDefinition.Methods.FirstOrDefault(x => x.Name == methodName &&
                                                          x.IsStatic &&
                                                          x.ReturnType.Name == "Boolean" &&
                                                          x.HasParameters &&
                                                          x.Parameters.Count == 2 &&
                                                          MatchParameter(x.Parameters[0], parameterType) &&
                                                          MatchParameter(x.Parameters[1], parameterType));

        if (reference == null && typeDefinition != parameterType)
            reference = FindNamedMethod(typeDefinition, methodName, typeDefinition);

        return reference;
>>>>>>> pr/1
    }

    static bool MatchParameter(ParameterDefinition parameter, TypeReference typeMatch)
    {
        if (parameter.ParameterType == typeMatch)
        {
            return true;
        }

        if (parameter.ParameterType.IsGenericInstance && typeMatch.IsGenericInstance)
        {
            return parameter.ParameterType.Resolve() == typeMatch.Resolve();
        }

        return false;
    }
}