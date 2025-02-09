﻿/*
   Copyright 2019 Christian Bender christian1.bender@student.uni-siegen.de

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;

namespace DCAKeyRecovery.Logic.Cipher3
{
    public class Cipher3DifferentialKeyRecoveryAttack : DifferentialKeyRecoveryAttack
    {
        //saves the already attacked SBoxes
        public bool[] attackedSBoxesRound5;
        public bool[] attackedSBoxesRound4;
        public bool[] attackedSBoxesRound3;
        public bool[] attackedSBoxesRound2;

        //indicates if a subkey is recovered
        public bool recoveredSubkey5;
        public bool recoveredSubkey4;
        public bool recoveredSubkey3;
        public bool recoveredSubkey2;
        public bool recoveredSubkey1;
        public bool recoveredSubkey0;

        //saves the recovered subkeys
        public ushort subkey5;
        public ushort subkey4;
        public ushort subkey3;
        public ushort subkey2;
        public ushort subkey1;
        public ushort subkey0;

        public Cipher3DifferentialKeyRecoveryAttack()
        {
            RoundConfigurations = new List<DifferentialAttackRoundConfiguration>();
            RoundResults = new List<DifferentialAttackRoundResult>();

            attackedSBoxesRound5 = new bool[4];
            attackedSBoxesRound4 = new bool[4];
            attackedSBoxesRound3 = new bool[4];
            attackedSBoxesRound2 = new bool[4];
        }

        public override string printRecoveredSubkeyBits()
        {
            throw new NotImplementedException();
        }
    }
}
