using System.Diagnostics;

namespace TableCloth3.Launcher.Contracts;

/// <summary>
/// Windows Sandbox 실행 전략을 정의하는 인터페이스
/// </summary>
public interface IWindowsSandboxExecutionStrategy
{
    /// <summary>
    /// 전략의 이름
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 기본 실행 방식 여부
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    /// 현재 환경에서 이 전략이 실행 가능한지 평가
    /// </summary>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>실행 가능 여부</returns>
    Task<bool> CanExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Windows Sandbox 프로필 파일을 실행
    /// </summary>
    /// <param name="profilePath">WSB 프로필 파일 경로</param>
    /// <param name="cancellationToken">취소 토큰</param>
    Task ExecuteAsync(string profilePath, CancellationToken cancellationToken = default);
}
