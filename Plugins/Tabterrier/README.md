# About
Plugin Tabterrier is developped by GreenM Team and allows to analyze tableau performance issues. 
Features: 
- Info about users activities.
- Dashboard open performance info.

# How to run:
1. [Download and setup logshark](https://github.com/tableau/Logshark/releases/tag/v2.1)
2. [Download plugin lib](https://github.com/greenmorg/Logshark/tree/tabterrier/Plugins/Tabterrier/Tabterrier.dll) and put it into Plugins directory in your Loghark installation: ../Logshark/Plugins/
3. Run Logshark with Tabterrier plugin:
```sh
logshark.exe mylogs.zip --plugins Tabterrier
```
4. You will find the results in ../Logshark/Output/ directory.