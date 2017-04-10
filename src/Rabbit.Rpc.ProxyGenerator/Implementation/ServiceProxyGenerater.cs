using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Rabbit.Rpc.Convertibles;
using Rabbit.Rpc.Ids;
using Rabbit.Rpc.ProxyGenerator.Utilitys;
using Rabbit.Rpc.Runtime.Client;
using Rabbit.Rpc.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if !NET

using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;

#endif

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Rabbit.Rpc.ProxyGenerator.Implementation
{
    public class ServiceProxyGenerater : IServiceProxyGenerater
    {
        #region Field

        private readonly IServiceIdGenerator _serviceIdGenerator;
        private readonly ILogger<ServiceProxyGenerater> _logger;

        #endregion Field

        #region Constructor

        public ServiceProxyGenerater(IServiceIdGenerator serviceIdGenerator, ILogger<ServiceProxyGenerater> logger)
        {
            _serviceIdGenerator = serviceIdGenerator;
            _logger = logger;
        }

        #endregion Constructor

        #region Implementation of IServiceProxyGenerater

        /// <summary>
        /// 生成服务代理。
        /// </summary>
        /// <param name="interfacTypes">需要被代理的接口类型。</param>
        /// <returns>服务代理实现。</returns>
        public IEnumerable<Type> GenerateProxys(IEnumerable<Type> interfacTypes)
        {
#if NET
            var assemblys = AppDomain.CurrentDomain.GetAssemblies();
#else
            var assemblys = DependencyContext.Default.RuntimeLibraries.SelectMany(i => i.GetDefaultAssemblyNames(DependencyContext.Default).Select(z => Assembly.Load(new AssemblyName(z.Name))));
#endif
            assemblys = assemblys.Where(i => i.IsDynamic == false).ToArray();
            var trees = interfacTypes.Select(GenerateProxyTree).ToList();
            var stream = CompilationUtilitys.CompileClientProxy(trees,
                assemblys
                    .Select(a => MetadataReference.CreateFromFile(a.Location))
                    .Concat(new[]
                    {
                        MetadataReference.CreateFromFile(typeof(Task).GetTypeInfo().Assembly.Location)
                    }),
                _logger);

            using (stream)
            {
#if NET
                var assembly = Assembly.Load(stream.ToArray());
#else
                var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
#endif

                return assembly.GetExportedTypes();
            }
        }

        /// <summary>
        /// 生成服务代理代码树。
        /// </summary>
        /// <param name="interfaceType">需要被代理的接口类型。</param>
        /// <returns>代码树。</returns>
        public SyntaxTree GenerateProxyTree(Type interfaceType)
        {
            var className = interfaceType.Name.Split('`')[0];
            if (className.StartsWith("I"))
                className = className.Substring(1);
            className += "ClientProxy";

            var members = new List<MemberDeclarationSyntax>
            {
                GetConstructorDeclaration(className)
            };

            members.AddRange(GenerateMethodDeclarations(interfaceType.GetMethods()));

            var classDec = ClassDeclaration(className)
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBaseList(
                    BaseList(
                        SeparatedList<BaseTypeSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                SimpleBaseType(IdentifierName("ServiceProxyBase")),
                                Token(SyntaxKind.CommaToken),
                                SimpleBaseType(GetTypeSyntaxAuto(interfaceType))
                            })))
                .WithMembers(List(members));

            {
                var gArgs = interfaceType.GetGenericArguments();
                if (gArgs.Length > 0)
                {
                    var typeParameterList = new List<TypeParameterSyntax>();
                    var constraintClauses = new List<TypeParameterConstraintClauseSyntax>();
                    foreach (var g in gArgs)
                    {
                        if (!g.IsGenericParameter) continue;
                        var typeParam = TypeParameter(g.Name);
                        var gi = g.GetTypeInfo();
                        var gAttr = gi.GenericParameterAttributes;
                        var constraints = new List<TypeParameterConstraintSyntax>();
                        //First
                        if ((gAttr & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                            constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
                        if ((gAttr & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                            constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));

                        //处理逆变, 协变
                        if ((gAttr & GenericParameterAttributes.Covariant) != 0)
                            typeParam = typeParam.WithVarianceKeyword(Token(SyntaxKind.OutKeyword));
                        if ((gAttr & GenericParameterAttributes.Contravariant) != 0)
                            typeParam = typeParam.WithVarianceKeyword(Token(SyntaxKind.InKeyword));
                        typeParameterList.Add(typeParam);

                        //base, interface
                        /*if (gi.BaseType != typeof(object))
                        {
                            constraints.Add(TypeConstraint(GetTypeSyntaxAuto(gi.BaseType)));
                        }*/
                        //NOTE: 不用Interfaces(因为这里找到的还包含了继承链上的), 但注意GetGenericParameterConstraints()实际上是包含了父类的
                        var interfaces = gi.GetGenericParameterConstraints();
                        foreach (var inter in interfaces)
                        {
                            //跳过struct约束(特殊约束: Object, ValueType, Enum, Array, Delegate)
                            if (inter == typeof(ValueType)) continue;
                            constraints.Add(TypeConstraint(GetTypeSyntaxAuto(inter)));
                        }
                        //Last
                        //NOTE: struct约束隐含了object约束
                        if ((gAttr & (GenericParameterAttributes.DefaultConstructorConstraint | GenericParameterAttributes.NotNullableValueTypeConstraint)) == GenericParameterAttributes.DefaultConstructorConstraint)
                            constraints.Add(ConstructorConstraint());
                        if (constraints.Count > 0)
                            constraintClauses.Add(TypeParameterConstraintClause(g.Name)
                                .WithConstraints(
                                    SeparatedList(constraints)));
                    }
                    if (typeParameterList.Count > 0)
                        classDec = classDec
                            .WithTypeParameterList(
                                TypeParameterList(
                                    SeparatedList<TypeParameterSyntax>(typeParameterList)));
                    if (constraintClauses.Count > 0)
                        classDec = classDec.WithConstraintClauses(
                            List<TypeParameterConstraintClauseSyntax>(constraintClauses));

                }
            }
            return CompilationUnit()
                .WithUsings(GetUsings())
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        NamespaceDeclaration(
                            QualifiedName(
                                QualifiedName(
                                    IdentifierName("Rabbit"),
                                    IdentifierName("Rpc")),
                                IdentifierName("ClientProxys")))
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(
                        classDec))))
                .NormalizeWhitespace().SyntaxTree;
        }

        #endregion Implementation of IServiceProxyGenerater

        #region Private Method
        private static readonly Dictionary<Type, SyntaxKind> _predefinedTypes = new Dictionary<Type, SyntaxKind>
        {
            [typeof(bool)] = SyntaxKind.BoolKeyword,
            [typeof(byte)] = SyntaxKind.ByteKeyword,
            [typeof(sbyte)] = SyntaxKind.SByteKeyword,
            [typeof(short)] = SyntaxKind.ShortKeyword,
            [typeof(ushort)] = SyntaxKind.UShortKeyword,
            [typeof(int)] = SyntaxKind.IntKeyword,
            [typeof(uint)] = SyntaxKind.UIntKeyword,
            [typeof(long)] = SyntaxKind.LongKeyword,
            [typeof(ulong)] = SyntaxKind.ULongKeyword,
            [typeof(double)] = SyntaxKind.DoubleKeyword,
            [typeof(float)] = SyntaxKind.FloatKeyword,
            [typeof(decimal)] = SyntaxKind.DecimalKeyword,
            [typeof(string)] = SyntaxKind.StringKeyword,
            [typeof(char)] = SyntaxKind.CharKeyword,
            [typeof(void)] = SyntaxKind.VoidKeyword,
            [typeof(object)] = SyntaxKind.ObjectKeyword,
        };
        private static TypeSyntax GetTypeSyntaxAuto(Type type)
        {
            if (_predefinedTypes.TryGetValue(type, out var predefinedSyntax))
            {
                return PredefinedType(Token(predefinedSyntax));
            }

            var ti = type.GetTypeInfo();
            if (ti.IsArray)
            {
                return ArrayType(GetTypeSyntaxAuto(type.GetElementType()));
            }
            else if(ti.IsGenericParameter)
            {
                //TODO: 泛型函数的参数
                return IdentifierName(ti.Name);
            }
            else if (ti.IsPointer)
            {
                //unsafe指针
                return PointerType(GetTypeSyntaxAuto(type.GetElementType()));
            }
            else if (ti.IsByRef)
            {
                //已在参数列表中处理
                throw new NotSupportedException();
            }

            var fullName = ti.GetSafeFullName().Split('`')[0];
            var parts = fullName.Split('.');

            SimpleNameSyntax baseSyntax;
            if (ti.IsGenericType)
            {
                //泛型类型
                baseSyntax = GenericName(parts.Last())
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SeparatedList<TypeSyntax>(
                                ti.GetGenericArguments()
                                    .Select(GetTypeSyntaxAuto))));
            }
            else
            {
                //普通类型
                baseSyntax = IdentifierName(parts.Last());
            }
            NameSyntax curSyntax = baseSyntax;
            if (parts.Length > 1)
            {
                //多段
                var partsSyntax = parts.Select(IdentifierName).ToArray<SimpleNameSyntax>();
                var len = partsSyntax.Length;
                partsSyntax[len - 1] = baseSyntax;
                curSyntax = partsSyntax[0];
                for (var i = 1; i < len; ++i)
                {
                    curSyntax = QualifiedName(curSyntax, partsSyntax[i]);
                }
            }
            return curSyntax;
        }

        private static QualifiedNameSyntax GetQualifiedNameSyntax(string fullName)
        {
            return GetQualifiedNameSyntax(fullName.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static QualifiedNameSyntax GetQualifiedNameSyntax(IReadOnlyCollection<string> names)
        {
            var ids = names.Select(IdentifierName).ToArray();

            var index = 0;
            QualifiedNameSyntax left = null;
            while (index + 1 < names.Count)
            {
                left = left == null ? QualifiedName(ids[index], ids[index + 1]) : QualifiedName(left, ids[index + 1]);
                index++;
            }
            return left;
        }

        private static SyntaxList<UsingDirectiveSyntax> GetUsings()
        {
            return List(
                new[]
                {
                    UsingDirective(IdentifierName("System")),
                    UsingDirective(GetQualifiedNameSyntax("System.Threading.Tasks")),
                    UsingDirective(GetQualifiedNameSyntax("System.Collections.Generic")),
                    UsingDirective(GetQualifiedNameSyntax(typeof(ITypeConvertibleService).Namespace)),
                    UsingDirective(GetQualifiedNameSyntax(typeof(IRemoteInvokeService).Namespace)),
                    UsingDirective(GetQualifiedNameSyntax(typeof(ISerializer<>).Namespace)),
                    UsingDirective(GetQualifiedNameSyntax(typeof(ServiceProxyBase).Namespace))
                });
        }

        private static ConstructorDeclarationSyntax GetConstructorDeclaration(string className)
        {
            return ConstructorDeclaration(Identifier(className))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(
                    ParameterList(
                        SeparatedList<ParameterSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                Parameter(
                                    Identifier("remoteInvokeService"))
                                    .WithType(
                                        IdentifierName("IRemoteInvokeService")),
                                Token(SyntaxKind.CommaToken),
                                Parameter(
                                    Identifier("typeConvertibleService"))
                                    .WithType(
                                        IdentifierName("ITypeConvertibleService"))
                            })))
                .WithInitializer(
                        ConstructorInitializer(
                            SyntaxKind.BaseConstructorInitializer,
                            ArgumentList(
                                SeparatedList<ArgumentSyntax>(
                                    new SyntaxNodeOrToken[]{
                                        Argument(
                                            IdentifierName("remoteInvokeService")),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(
                                            IdentifierName("typeConvertibleService"))}))))
                .WithBody(Block());
        }

        private IEnumerable<MemberDeclarationSyntax> GenerateMethodDeclarations(IEnumerable<MethodInfo> methods)
        {
            var array = methods.ToArray();
            return array.Select(GenerateMethodDeclaration).ToArray();
        }

        private MemberDeclarationSyntax GenerateMethodDeclaration(MethodInfo method)
        {
            //感觉roslyn比Emit还难玩啊...
            var serviceId = _serviceIdGenerator.GenerateServiceId(method);
            var returnDeclaration = GetTypeSyntaxAuto(method.ReturnType);

            var parameterList = new List<SyntaxNodeOrToken>();
            var parameterDeclarationList = new List<SyntaxNodeOrToken>();

            foreach (var parameter in method.GetParameters())
            {
                var paramType = parameter.ParameterType;
                var paramSyntax = Parameter(Identifier(parameter.Name));
                //只是试一下写法(实际不支持)
                if (paramType.IsByRef)
                {
                    paramType = paramType.GetElementType();
                    if (parameter.IsOut)
                    {
                        paramSyntax = paramSyntax.WithModifiers(TokenList(Token(SyntaxKind.OutKeyword)));
                    }
                    else
                    {
                        paramSyntax = paramSyntax.WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)));
                    }
                }
                paramSyntax = paramSyntax.WithType(GetTypeSyntaxAuto(paramType));
                parameterDeclarationList.Add(paramSyntax);
                parameterDeclarationList.Add(Token(SyntaxKind.CommaToken));

                parameterList.Add(InitializerExpression(
                    SyntaxKind.ComplexElementInitializerExpression,
                    SeparatedList<ExpressionSyntax>(
                        new SyntaxNodeOrToken[]{
                            LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                Literal(parameter.Name)),
                            Token(SyntaxKind.CommaToken),
                            IdentifierName(parameter.Name)})));
                parameterList.Add(Token(SyntaxKind.CommaToken));
            }
            if (parameterList.Any())
            {
                parameterList.RemoveAt(parameterList.Count - 1);
                parameterDeclarationList.RemoveAt(parameterDeclarationList.Count - 1);
            }

            var declaration = MethodDeclaration(
                returnDeclaration,
                Identifier(method.Name))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword)))
                .WithParameterList(ParameterList(SeparatedList<ParameterSyntax>(parameterDeclarationList)));
            //TODO: 测试下unsafe的识别(实际不支持)
            {
                var gArgs = method.GetGenericArguments();
                if (gArgs.Length > 0)
                {
                    var typeParameterList = new List<TypeParameterSyntax>();
                    var constraintClauses = new List<TypeParameterConstraintClauseSyntax>();
                    foreach (var g in gArgs)
                    {
                        if (!g.IsGenericParameter) continue;

                        typeParameterList.Add(TypeParameter(g.Name));
                        var gi = g.GetTypeInfo();
                        var gAttr = gi.GenericParameterAttributes;
                        var constraints = new List<TypeParameterConstraintSyntax>();
                        //First
                        if ((gAttr & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
                            constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));
                        if ((gAttr & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                            constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));

                        //TODO: 逆变, 协变
                        if ((gAttr & GenericParameterAttributes.Covariant) != 0)
                            throw new NotSupportedException();
                        if ((gAttr & GenericParameterAttributes.Contravariant) != 0)
                            throw new NotSupportedException();

                        //base, interface
                        /*if (gi.BaseType != typeof(object))
                        {
                            constraints.Add(TypeConstraint(GetTypeSyntaxAuto(gi.BaseType)));
                        }*/
                        //NOTE: 不用Interfaces(因为这里找到的还包含了继承链上的), 但注意GetGenericParameterConstraints()实际上是包含了父类的
                        var interfaces = gi.GetGenericParameterConstraints();
                        foreach (var inter in interfaces)
                        {
                            //跳过struct约束(特殊约束: Object, ValueType, Enum, Array, Delegate)
                            if (inter == typeof(ValueType)) continue;
                            constraints.Add(TypeConstraint(GetTypeSyntaxAuto(inter)));
                        }
                        //Last
                        //NOTE: struct约束隐含了object约束
                        if ((gAttr & (GenericParameterAttributes.DefaultConstructorConstraint | GenericParameterAttributes.NotNullableValueTypeConstraint)) == GenericParameterAttributes.DefaultConstructorConstraint)
                            constraints.Add(ConstructorConstraint());
                        if (constraints.Count > 0)
                            constraintClauses.Add(TypeParameterConstraintClause(g.Name)
                                .WithConstraints(
                                    SeparatedList(constraints)));
                    }
                    if (typeParameterList.Count > 0)
                        declaration = declaration
                            .WithTypeParameterList(
                                TypeParameterList(
                                    SeparatedList<TypeParameterSyntax>(typeParameterList)));
                    if (constraintClauses.Count > 0)
                        declaration = declaration.WithConstraintClauses(
                            List<TypeParameterConstraintClauseSyntax>(constraintClauses));

                }
            }

            ExpressionSyntax expressionSyntax;
            StatementSyntax statementSyntax;

            //TODO: 尝试下支持同步返回
            if (method.ReturnType.GetTypeInfo().IsGenericType)//!= typeof(Task)
            {
                expressionSyntax = GenericName(
                    Identifier("Invoke")).WithTypeArgumentList(returnDeclaration.GetInnerTypeArgumentList());
            }
            else
            {
                expressionSyntax = IdentifierName("Invoke");
            }
            expressionSyntax = AwaitExpression(
                InvocationExpression(expressionSyntax)
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]
                                {
                                        Argument(
                                            ObjectCreationExpression(
                                                GenericName(
                                                    Identifier("Dictionary"))
                                                    .WithTypeArgumentList(
                                                        TypeArgumentList(
                                                            SeparatedList<TypeSyntax>(
                                                                new SyntaxNodeOrToken[]
                                                                {
                                                                    PredefinedType(
                                                                        Token(SyntaxKind.StringKeyword)),
                                                                    Token(SyntaxKind.CommaToken),
                                                                    PredefinedType(
                                                                        Token(SyntaxKind.ObjectKeyword))
                                                                }))))
                                                .WithInitializer(
                                                    InitializerExpression(
                                                        SyntaxKind.CollectionInitializerExpression,
                                                        SeparatedList<ExpressionSyntax>(
                                                            parameterList)))),
                                        Token(SyntaxKind.CommaToken),
                                        Argument(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(serviceId)))
                                }))));

            if (method.ReturnType != typeof(Task))
            {
                statementSyntax = ReturnStatement(expressionSyntax);
            }
            else
            {
                statementSyntax = ExpressionStatement(expressionSyntax);
            }

            declaration = declaration.WithBody(
                        Block(
                            SingletonList(statementSyntax)));

            return declaration;
        }

#endregion Private Method
    }

    public static class ServiceProxyGeneraterUtils
    {
        public static TypeArgumentListSyntax GetInnerTypeArgumentList(this TypeSyntax that)
        {
            if (that is GenericNameSyntax)
            {
                return ((GenericNameSyntax)that).TypeArgumentList;
            }
            else if (that is QualifiedNameSyntax)
            {
                return ((QualifiedNameSyntax)that).Right.GetInnerTypeArgumentList();
            }
            else
            {
                return null;
            }
        }
        public static string GetSafeFullName(this System.Reflection.TypeInfo that)
        {
            //TODO: 测试下是否可以直接ToString()
            if (that.IsGenericParameter)
                return that.Name;

            //T, T[], Task<T>等(详见文档)的FullName为null, 转手动
            //NOTE: 注意Nested Type的默认分隔符为'+', 做一下替换
            var fullname = that.FullName?.Replace('+', '.');
            if (fullname != null)
                return fullname;

            //检查是否为Nested Type
            var parent = that.DeclaringType;
            if (parent == null)
                return that.Namespace + "." + that.Name;
            return parent.GetTypeInfo().GetSafeFullName() + "." + that.Name;
        }
    }
}