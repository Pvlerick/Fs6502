namespace Fs6502

    type Memory() =
        member private this.memory = Array.create 256 0uy

        member this.Item
            with get(address: byte) = this.memory.[System.BitConverter.ToInt32([|address|], 0)]
            and internal set(address: byte) value = this.memory.[System.BitConverter.ToInt32([|address|], 0)] <- value