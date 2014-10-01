namespace Fs6502

    type private Modes =
        | Immediate of Arguments
        | ZeroPage of Arguments
        | ZeroPageX of Arguments
        | Absolute of Arguments
        | AbsoluteX of Arguments
        | AbsoluteY of Arguments
        | IndirectX of Arguments
        | IndirectY of Arguments