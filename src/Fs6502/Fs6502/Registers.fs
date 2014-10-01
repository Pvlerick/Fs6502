namespace Fs6502
    
    type Registers() =
        member val public Accumulator = 0uy with get, set
        member val public IndexX = 0uy with get, set
        member val public IndexY = 0uy with get, set

