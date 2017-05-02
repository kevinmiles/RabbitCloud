# 泛型支持
* [ ] 支持泛型类型
* [ ] 支持嵌套类型的祖先是泛型类型
* [ ] 支持方法的泛型参数
* [ ] 避免泛型造成serviceId冲突

# 异步支持
* [x] 支持ValueTask<T>
* [ ] 支持其他可await自定义异步类型(实现了`GetAwait()`且支持从Task转换)
* [x] 支持同步返回
* [ ] 支持CancellationToken
* [ ] 对可能的情况, 直接返回Task而不做多余的await
* [ ] (同步返回)支持out和ref参数
* [ ] void做同步返回(通过接口无法区分方法是否是async定义)

# 代码生成(Roslyn)
* [x] 正确处理返回多级泛型的情况
* [x] 简化常见类型的代码生成, `System.Int32` -> `int`等
* [ ] 优化using
* [ ] 优化dll引用

# 基架
* [ ] 对于Core和Framework中int在不同程序集中定义的情况不出错
* [ ] 在ServiceProxyBase中提供同步返回的实现

# 跨语言?
* [ ] 支持自定义serviceId
* [ ] 支持自定义Type映射

# 其他
* [x] 修复接口与实现类的方法参数名不一致造成的问题
* [x] 修正接口的继承/override问题