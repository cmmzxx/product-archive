namespace TopSolidMingdao.Core;

// 只有经过服务层整理、适合向用户展示的错误才能使用此类型。
public sealed class MingdaoServiceException(string message) : Exception(message)
{
}
