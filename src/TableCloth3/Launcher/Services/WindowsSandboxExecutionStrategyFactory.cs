using TableCloth3.Launcher.Contracts;

namespace TableCloth3.Launcher.Services;

/// <summary>
/// Windows Sandbox 실행 전략을 선택하는 팩토리
/// </summary>
public sealed class WindowsSandboxExecutionStrategyFactory
{
    private readonly IEnumerable<IWindowsSandboxExecutionStrategy> _strategies;

    public WindowsSandboxExecutionStrategyFactory(IEnumerable<IWindowsSandboxExecutionStrategy> strategies)
    {
        _strategies = strategies;
    }

    /// <summary>
    /// 사용 가능한 전략을 평가하고 최적의 전략을 선택
    /// </summary>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>선택된 전략과 기본 전략 사용 가능 여부</returns>
    public async Task<(IWindowsSandboxExecutionStrategy Strategy, bool IsPreferredAvailable)> SelectStrategyAsync(
        CancellationToken cancellationToken = default)
    {
        var availableStrategies = new List<IWindowsSandboxExecutionStrategy>();

        // 모든 전략의 실행 가능 여부를 확인
        foreach (var strategy in _strategies)
        {
            if (await strategy.CanExecuteAsync(cancellationToken).ConfigureAwait(false))
            {
                availableStrategies.Add(strategy);
            }
        }

        if (availableStrategies.Count == 0)
        {
            throw new InvalidOperationException(
                "No available Windows Sandbox execution strategy found. " +
                "Please ensure Windows Sandbox is enabled on your system.");
        }

        // 기본 전략(IsPreferred = true)을 우선 선택
        var preferredStrategy = availableStrategies.FirstOrDefault(s => s.IsPreferred);

        if (preferredStrategy != null)
        {
            return (preferredStrategy, true);
        }

        // 기본 전략을 사용할 수 없는 경우 대체 전략 선택
        var fallbackStrategy = availableStrategies.First();
        return (fallbackStrategy, false);
    }

    /// <summary>
    /// 기본 전략의 사용 가능 여부만 확인
    /// </summary>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>기본 전략 사용 가능 여부</returns>
    public async Task<bool> IsPreferredStrategyAvailableAsync(CancellationToken cancellationToken = default)
    {
        var preferredStrategy = _strategies.FirstOrDefault(s => s.IsPreferred);

        if (preferredStrategy == null)
            return false;

        return await preferredStrategy.CanExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
