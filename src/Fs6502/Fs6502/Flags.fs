namespace Fs6502
    
    type Flags() =
        member private this.status = [| false; false; true; false; false; false; false; false |]

        member this.Carry
            with internal get() = this.status.[7]
            and private set(value) = this.status.[7] <- value
        member this.Zero
            with internal get() = this.status.[6]
            and private set(value) = this.status.[6] <- value
        member this.Interrupt
            with internal get() = this.status.[5]
            and private set(value) = this.status.[5] <- value
        member this.Decimal
            with internal get() = this.status.[4]
            and private set(value) = this.status.[4] <- value
        member this.SoftwareInterrupt
            with internal get() = this.status.[2]
            and private set(value) = this.status.[2] <- value
        member this.Overflow
            with internal get() = this.status.[1]
            and private set(value) = this.status.[1] <- value
        member this.Sign
            with internal get() = this.status.[0]
            and private set(value) = this.status.[0] <- value