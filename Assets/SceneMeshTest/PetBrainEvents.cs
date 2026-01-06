using System;
using System.Collections.Generic;

public static class PetBrainEvents
{
    // Brain 쪽에 "다음 행동 만들어!"를 요청
    public static event Action BrainTickRequested;

    // Brain이 LLMCommands.json을 작성 완료했음을 알림
    public static event Action<CommandsCommittedArgs> CommandsCommitted;

    // Action 쪽에서 "이전 행동 끝남"을 알림 (Action 파트가 Raise 해줘야 함)
    public static event Action<ActionFinishedArgs> ActionFinished;

    public static void RequestBrainTick()
        => BrainTickRequested?.Invoke();

    public static void RaiseCommandsCommitted(string path, int commandCount, string reason)
        => CommandsCommitted?.Invoke(new CommandsCommittedArgs(path, commandCount, reason));

    public static void RaiseActionFinished(bool success, string reason, string lastAction)
        => ActionFinished?.Invoke(new ActionFinishedArgs(success, reason, lastAction));
}

public readonly struct CommandsCommittedArgs
{
    public readonly string path;
    public readonly int commandCount;
    public readonly string reason;

    public CommandsCommittedArgs(string path, int commandCount, string reason)
    {
        this.path = path;
        this.commandCount = commandCount;
        this.reason = reason;
    }
}

public readonly struct ActionFinishedArgs
{
    public readonly bool success;
    public readonly string reason;
    public readonly string lastAction;

    public ActionFinishedArgs(bool success, string reason, string lastAction)
    {
        this.success = success;
        this.reason = reason;
        this.lastAction = lastAction;
    }
}
