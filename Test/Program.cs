using MediDocParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var medidocParser = new Parser();

            //Rapporten:DMA-REP 
            //Radiologie: DMA-IMA 
            //Labo:DMA-LAB 

            var text = @"1/07389/87/870
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

            var executingDocters = medidocParser.ParseReport(text); //DMA-REP
        }
    }
}
