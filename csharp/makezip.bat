rd /s/q obj
rd /s/q bin
rd /s/q Guess\obj
rd /s/q Guess\bin
rd /s/q TransparentGuess\obj
rd /s/q TransparentGuess\bin
rd /s/q PropGuess\obj
rd /s/q PropGuess\bin
rd /s/q TestIndex\obj
rd /s/q TestIndex\bin
rd /s/q TestIndex2\obj
rd /s/q TestIndex2\bin
rd /s/q TestLink\obj
rd /s/q TestLink\bin
rd /s/q TestGC\obj
rd /s/q TestGC\bin
rd /s/q TestConcur\obj
rd /s/q TestConcur\bin
rd /s/q TestRtree\obj
rd /s/q TestRtree\bin
rd /s/q TestR2\obj
rd /s/q TestR2\bin
rd /s/q TestTtree\obj
rd /s/q TestTtree\bin
rd /s/q TestXML\obj
rd /s/q TestXML\bin
rd /s/q TestBackup\obj
rd /s/q TestBackup\bin
rd /s/q TestRaw\obj
rd /s/q TestRaw\bin
rd /s/q TestSSD\obj
rd /s/q TestSSD\bin
rd /s/q TestSOD\obj
rd /s/q TestSOD\bin
rd /s/q TestEnumerator\obj
rd /s/q TestEnumerator\bin
rd /s/q TestCompoundIndex\obj
rd /s/q TestCompoundIndex\bin
rd /s/q TestBlob\obj
rd /s/q TestBlob\bin
rd /s/q TestBit\obj
rd /s/q TestBit\bin
del /q *.dbs
rd /s/q TestTimeSeries\obj
rd /s/q TestTimeSeries\bin
del /q *.xml
rd /s/q IpCountry\obj
rd /s/q IpCountry\bin
del /q IpCountry\*.dbs
del /q *.suo
del /q *.ncb
cd ..
del /q perstnet.zip
zip -r perstnet.zip Perst.NET
