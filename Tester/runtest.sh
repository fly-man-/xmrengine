#!/bin/bash -v
#
#  Tests the XMREngine compiler by running scripts, supplying inputs and checking the outputs.
#
#    ./runtest.sh mmr       uses the micro thread model (requires patched mono)
#    ./runtest.sh con       uses the continuations thread model (requires mono)
#    ./runtest.sh sys       uses the system threads model (works with windows and mono)
#
function majorbar {
    echo ===============================================================================
}
function minorbar {
    echo = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = = =
}

function makerealgood {
    compdir=`pwd -P`
    compdir=${compdir//\//\\/}
    sed "s/in \/.*\/XMREngine\/Compiler\//in $compdir\//g" $1.good > $1.good.tmp
}

function testit {
    makerealgood $1
    input=/dev/null
    if [ -f $1.in ]
    then
        input=$1.in
    fi
    $runxmrengtest -xmrasm $1.lsl < $input 2>&1 | tee $1.out
    diff $1.out $1.good.tmp
    minorbar
    $runxmrengtest -checkrun $1.lsl < $input 2>&1 | tee $1.ckr
    diff $1.ckr $1.good.tmp
    minorbar
    $runxmrengtest -serialize $1.lsl < $input 2>&1 | tee $1.ser
    diff $1.ser $1.good.tmp
    majorbar
    rm -f $1.good.tmp
}

function testev {
    makerealgood $1
    $runxmrengtest -eventio $1.lsl < $1.events 2>&1 | tee $1.out
    diff $1.out $1.good.tmp
    minorbar
    $runxmrengtest -eventio -checkrun $1.lsl < $1.events 2>&1 | tee $1.ckr
    diff $1.ckr $1.good.tmp
    minorbar
    $runxmrengtest -eventio -serialize $1.lsl < $1.events 2>&1 | tee $1.ser
    diff $1.ser $1.good.tmp
    majorbar
    rm -f $1.good.tmp
}

set -e
cd `dirname $0`
time make xmrengtest.exe
export MONO_PATH=osbin

if [ "$MONODIR" != "" ]
then
    runxmrengtest="$MONODIR/bin/mono --debug xmrengtest.exe"
else
    runxmrengtest="mono --debug xmrengtest.exe"
fi
echo runxmrengtest=$runxmrengtest

majorbar
testit scriptdbtest
testit misctest
testit overridevar
testit melbuttons
testev buildersbuddy
testit listinserttest
testit arraytest
testit chartest
testit fieldtest
testit keytest
testit objectest
testit proptest
testit trycatchexample
testit trycatchtest
testit while1
testit xmrstringfunctest
testit condtest
testev testllparcelmedia
testit typeassigntest
testit partialtest
testev statechange
testev lslangtest1
testev lslangtest2

majorbar
$runxmrengtest -xmrasm -eventio -ipcchannel -2135482309 \
    -primname controller -primuuid 0000 ipctest0.lsl \
    -primname player1 -primuuid 1111 ipctest1.lsl << EOF | tee ipctest.out
1)touch_start(0)
"0000")llListRandomize:69827238
"1111")touch_start(0)
touch_start(0)
touch_start(0)
EOF
diff ipctest.out ipctest.good

minorbar
$runxmrengtest -eventio -ipcchannel -2135482309 -checkrun \
    -primname controller -primuuid 0000 ipctest0.lsl \
    -primname player1 -primuuid 1111 ipctest1.lsl << EOF | tee ipctest.ckr
1)touch_start(0)
0)llListRandomize:69827238
1)touch_start(0)
touch_start(0)
touch_start(0)
EOF
diff ipctest.ckr ipctest.gser

minorbar
$runxmrengtest -eventio -ipcchannel -2135482309 -serialize \
    -primname controller -primuuid 0000 ipctest0.lsl \
    -primname player1 -primuuid 1111 ipctest1.lsl << EOF | tee ipctest.ser
1)touch_start(0)
0)llListRandomize:69827238
1)touch_start(0)
touch_start(0)
touch_start(0)
EOF
diff ipctest.ser ipctest.gser

majorbar
$runxmrengtest -eventio -ipcchannel -646830961 \
    -primname RummyBoard        -primuuid 1000 rummyboard.lsl \
    -primname Stock             -primuuid 1101 cardpanel.lsl \
    -primname Discard           -primuuid 1102 cardpanel.lsl \
    -primname HelpButton        -primuuid 1103 cardpanel.lsl \
    -primname ResetButton       -primuuid 1104 cardpanel.lsl \
    -primname StartButton       -primuuid 1105 cardpanel.lsl \
    -primname MeldPanel0        -primuuid 1200 cardpanel.lsl \
    -primname MeldCardPanel0    -primuuid 2000 cardpanel.lsl \
    -primname MeldCardPanel1    -primuuid 2001 cardpanel.lsl \
    -primname MeldCardPanel2    -primuuid 2002 cardpanel.lsl \
    -primname MeldCardPanel3    -primuuid 2003 cardpanel.lsl \
    -primname MeldPanel1        -primuuid 1201 cardpanel.lsl \
    -primname MeldCardPanel10   -primuuid 2010 cardpanel.lsl \
    -primname MeldCardPanel11   -primuuid 2011 cardpanel.lsl \
    -primname MeldCardPanel12   -primuuid 2012 cardpanel.lsl \
    -primname MeldCardPanel13   -primuuid 2013 cardpanel.lsl \
    -primname MeldPanel2        -primuuid 1202 cardpanel.lsl \
    -primname MeldCardPanel20   -primuuid 2020 cardpanel.lsl \
    -primname MeldCardPanel21   -primuuid 2021 cardpanel.lsl \
    -primname MeldCardPanel22   -primuuid 2022 cardpanel.lsl \
    -primname MeldCardPanel23   -primuuid 2023 cardpanel.lsl \
    -primname MeldPanel3        -primuuid 1203 cardpanel.lsl \
    -primname MeldCardPanel30   -primuuid 2030 cardpanel.lsl \
    -primname MeldCardPanel31   -primuuid 2031 cardpanel.lsl \
    -primname MeldCardPanel32   -primuuid 2032 cardpanel.lsl \
    -primname MeldCardPanel33   -primuuid 2033 cardpanel.lsl \
    -primname MeldPanel4        -primuuid 1204 cardpanel.lsl \
    -primname MeldCardPanel40   -primuuid 2040 cardpanel.lsl \
    -primname MeldCardPanel41   -primuuid 2041 cardpanel.lsl \
    -primname MeldCardPanel42   -primuuid 2042 cardpanel.lsl \
    -primname MeldCardPanel43   -primuuid 2043 cardpanel.lsl \
    -primname PlayerStatus0     -primuuid 1300 cardpanel.lsl \
    -primname PlayerStatus1     -primuuid 1301 cardpanel.lsl \
    -primname PlayerStatus2     -primuuid 1302 cardpanel.lsl \
    -primname PlayerStatus3     -primuuid 1303 cardpanel.lsl \
    -primname PlayerCardPanel0  -primuuid 3000 cardpanel.lsl \
    -primname PlayerCardPanel1  -primuuid 3001 cardpanel.lsl \
    -primname PlayerCardPanel2  -primuuid 3002 cardpanel.lsl \
    -primname PlayerCardPanel3  -primuuid 3003 cardpanel.lsl \
    -primname PlayerCardPanel4  -primuuid 3004 cardpanel.lsl \
    -primname PlayerCardPanel5  -primuuid 3005 cardpanel.lsl \
    -primname PlayerCardPanel6  -primuuid 3006 cardpanel.lsl \
    -primname PlayerCardPanel7  -primuuid 3007 cardpanel.lsl \
    -primname PlayerCardPanel8  -primuuid 3008 cardpanel.lsl \
    -primname PlayerCardPanel9  -primuuid 3009 cardpanel.lsl \
    -primname PlayerCardPanel10 -primuuid 3010 cardpanel.lsl \
    -primname PlayerCardPanel11 -primuuid 3011 cardpanel.lsl \
    -primname PlayerCardPanel12 -primuuid 3012 cardpanel.lsl \
    -primname PlayerCardPanel13 -primuuid 3013 cardpanel.lsl \
    -primname PlayerCardPanel14 -primuuid 3014 cardpanel.lsl \
    -primname PlayerCardPanel15 -primuuid 3015 cardpanel.lsl \
    -primname PlayerCardPanel16 -primuuid 3016 cardpanel.lsl \
    -primname PlayerCardPanel17 -primuuid 3017 cardpanel.lsl \
    -primname PlayerCardPanel18 -primuuid 3018 cardpanel.lsl \
    -primname PlayerCardPanel19 -primuuid 3019 cardpanel.lsl \
    -primname PlayerCardPanel0  -primuuid 3100 cardpanel.lsl \
    -primname PlayerCardPanel1  -primuuid 3101 cardpanel.lsl \
    -primname PlayerCardPanel2  -primuuid 3102 cardpanel.lsl \
    -primname PlayerCardPanel3  -primuuid 3103 cardpanel.lsl \
    -primname PlayerCardPanel4  -primuuid 3104 cardpanel.lsl \
    -primname PlayerCardPanel5  -primuuid 3105 cardpanel.lsl \
    -primname PlayerCardPanel6  -primuuid 3106 cardpanel.lsl \
    -primname PlayerCardPanel7  -primuuid 3107 cardpanel.lsl \
    -primname PlayerCardPanel8  -primuuid 3108 cardpanel.lsl \
    -primname PlayerCardPanel9  -primuuid 3109 cardpanel.lsl \
    -primname PlayerCardPanel10 -primuuid 3110 cardpanel.lsl \
    -primname PlayerCardPanel11 -primuuid 3111 cardpanel.lsl \
    -primname PlayerCardPanel12 -primuuid 3112 cardpanel.lsl \
    -primname PlayerCardPanel13 -primuuid 3113 cardpanel.lsl \
    -primname PlayerCardPanel14 -primuuid 3114 cardpanel.lsl \
    -primname PlayerCardPanel15 -primuuid 3115 cardpanel.lsl \
    -primname PlayerCardPanel16 -primuuid 3116 cardpanel.lsl \
    -primname PlayerCardPanel17 -primuuid 3117 cardpanel.lsl \
    -primname PlayerCardPanel18 -primuuid 3118 cardpanel.lsl \
    -primname PlayerCardPanel19 -primuuid 3119 cardpanel.lsl \
        < rummytest.in | tee rummytest.out
diff rummytest.out rummytest.good

majorbar
echo "SUCCESS"
