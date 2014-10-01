open Fs6502

let cpu = new Cpu()

let printState (cpu: Cpu) =
    printfn ""
    printfn "-- CPU State --"
    printfn "Accumulator: %A" cpu.Registers.Accumulator
    printfn "Index X: %A" cpu.Registers.IndexX
    printfn "Index Y: %A" cpu.Registers.IndexY
    printfn "---------------"
    printfn ""

let execute instruction =
    printfn "%s" instruction
    cpu.Execute instruction

[<EntryPoint>]
let main argv = 
    //printfn "%A" argv.[0]

    printState cpu

    execute "LDA #$05"
    execute "STA $05ef"
    execute "LDA #$05"
    execute "STA $0201"
    execute "LDA #$08"
    execute "STA $0202"

    printState cpu

    ignore (System.Console.ReadLine())

    0 // return an integer exit code