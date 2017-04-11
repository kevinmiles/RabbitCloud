# 泛型支持
* [ ] 支持泛型类型
* [ ] 支持嵌套类型的祖先是泛型类型
* [ ] 支持方法的泛型参数
* [ ] 避免泛型造成serviceId冲突

# 异步支持
* [ ] 支持其他可await自定义异步类型(实现了`GetAwait()`且支持从Task转换)
* [ ] 支持同步返回
* [ ] 支持CancellationToken
* [ ] 对可能的情况, 直接返回Task而不做多余的await
* [ ] (同步返回)支持out和ref参数

# 代码生成(Roslyn)
* [ ] 优化using
* [ ] 优化dll引用
