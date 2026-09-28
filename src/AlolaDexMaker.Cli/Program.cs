using System.Text;
using AlolaDexMaker.Cli;

// AlolaDexMaker.Cli           asks what it needs (Questions)
// AlolaDexMaker.Cli --refresh <save> <folder> [--seed n] [--only 590,591]      for whoever keeps the template (Mend)
// AlolaDexMaker.Cli [--name <어버이 이름>] [--english <영어 등 게임의 트레이너 이름>] [--japanese <일본어 게임의 트레이너 이름>] [--chinese <중국어 게임의 트레이너 이름>] [--sid <네 자리>] [--tid <여섯 자리>] [--ball <볼 이름|볼맞춤>] [--ivs <랜덤|5V>] [--color <일반|이로치>] [--sex <수컷|암컷|랜덤>] [--level <최저|100>] [--from yyyy-mm-dd] [--to yyyy-mm-dd] [--seed n] [--out <folder>]
//   shiny wherever a Pokemon can be, unless plain is asked for: then plain wherever it can be
//   the individual values come as they come (랜덤), or five perfect for whatever hatched (5V);
//   cards and what came up from older games keep what they are given
//   everything goes into one ball - a Poke Ball unless another is named - where it can, and into a Poke Ball where it cannot;
//   볼맞춤 asks instead for the balls picked for each Pokemon
//   the name is 미월 unless another is given; the two IDs are drawn unless given
//   the player is a girl: a boy needs a save begun as one, and there is none yet
Console.OutputEncoding = Encoding.UTF8;
if (OperatingSystem.IsWindows()) Console.InputEncoding = Encoding.Unicode;
return new Runner(Console.In, Console.Out, Console.Error, Environment.CurrentDirectory).Run(args);
