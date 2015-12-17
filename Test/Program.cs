using System;
using System.IO;
using System.Text;
using MediDocParser;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            //Rapporten:DMA-REP 
            //Radiologie: DMA-IMA 
            //Labo:DMA-LAB 

            /*var text = @"1/07389/87/870
VAN DORPE               JOHAN
WILGENSTRAAT 28                     
8850      Zwevezele


20140401
1/33313/62/140
Vuylsteke               Bernard
#A570617YBN.FRN
BOONE                   Pol
19570617
Y
20140331
P14/010247
70805003
Kanaalstraat 142   
8650   
HOUTHULST               
#Rb
!Huid (1)
AARD WEEFSEL : huid linker axilla. 

KLINISCHE GEGEVENS : 
Letsel linker axilla. 

MACROSCOPIE :  
- Aantal stalen 	:	één recipiënt met één fragment. 
- Afmetingen	:	12x7x3 mm. 	
- Procedure 	:	excisiebiopsie. 	
- Aantal blokken	:	één. 	
- Rest	:	nul. 	


MICROSCOPIE :  
Het fragment bestaat uit epiderm, derm en hypoderm.   Het letsel gaat uit
van het epiderm.
Het letsel toont papillomatose en acanthose.   Aan het oppervlak is er
hyperkeratose en parakeratose.   Er worden pseudohoorncysten gevormd.   De
samenstellende cellen komen wat immatuur voor. Er is geen noemenswaardige
nucleaire atypie.
Het letsel is gepigmenteerd.   Het derm toont een perivasculair vrij dens
mononucleair ontstekingsinfiltraat.   Het beeld is dat van een
gepigmenteerde seborroïsche keratose.   Daar waar beoordeelbaar zijn de
snedevlakken letselvrij. 

BESLUIT : 
Excisie letsel linker axilla : gepigmenteerde seborroïsche keratose.  
Geen argumenten voor dysplasie of maligniteit.

#R/
#A/
#/
";

            var executingDocters = medidocParser.ParseTextReport(text); //DMA-REP
            var json = JsonConvert.SerializeObject(executingDocters, Formatting.Indented);
            Console.WriteLine(json);

            //Thread.Sleep(2000);

            text = @"W841
Medisch Laboratorium
Van Iseghemlaan 98
8400 Oostende
Tel. (059) 50.86.95
RIZIV 1/22222/33/444
19480601
1/12345/12/123
JANNSENS                KAREL           
#A130311YVVLTHF
VAN DE VELDE            THEOFIEL        
19130311
Y
19980608
10000000000001
C
#Rc
0100....
#R/
#Rc
13713..B
Commentaar bij de formule : hypersegmentatie
                            anisocytose +
                            enkele polychromatische RBC
#R/
#Ra
13713A.B
=0
01
N
#R/
#Ra
13713B.B
=85
01
H
#R/
#Ra
13713C.B
=1
01
N
#R/
#Ra
13713D.B
=0
01
N
#R/
#Ra
13713E.B
=11
01
L
#R/
#Ra
13713F.B
=3
01
N
#R/
#Rc
0120....
#R/
#Ra
13513A.B
=52.6
40
HH
#R/
#Ra
57363A.U
=8.1
40
H
#R/
#Ra
57215A.B
=3.9
75
LL
#R/
#Ra
13551A.B
=83
78
H
#R/
#Ra
13553A.B
=74
78
H
#R/
#Ra
57277A.B
=60
78
H
#R/
#Ra
57283A.B
=452
78
H
#R/
#Ra
57275A.B
=8
78
N
#R/
#Ra
57269A.B
=601
78
H
#R/
#A/
#/119
";

            var labs = medidocParser.ParseLabReport(text); //DMA-LAB
            json = JsonConvert.SerializeObject(labs, Formatting.Indented);
            Console.WriteLine(json);
            */

            var text = File.ReadAllText(@"E:\Temp\labo01.lab");

            var labs = Parser.ParseLabReport(text); //DMA-LAB

            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
                Composer.ComposeLabReport(writer, labs);

            Console.WriteLine(sb.ToString());


            Console.ReadLine();
        }
    }
}
