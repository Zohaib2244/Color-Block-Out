using System;

public enum BlockColorTypes
{
    Red,
    Orange,
    Yellow,
    Blue,
    Cyan,
    Green,
    Purple,
    Pink,
    Teal
}

public static class EnumExtensions
{
    //* Returns the number of values in any enum
    public static int Count<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T)).Length;
    }
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right,
}
public enum ScreenType
{
    MainMenu,
    LevelSelection,
    GamePlay,
    Settings,
    GameOver,
    LevelCompleted,
    Loading,
    Pause,
    NoInternet,
}
public enum LevelState
{
    None,
    InProgress,
    Completed,
    Failed,
}
public enum HoleType
{
    Isolated,   //* 0 connections
    EndCap,     //* 1 connection
    Straight,   //* 2 opposite connections
    Corner,     //* 2 adjacent connections
    One_Side,   //* 3 connections
    Middle      //* 4 connections
}