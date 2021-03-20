# THIS IS WORKING JUST FOR MY USAGE.  PLEASE USE THIS AS INSPIRATION FOR YOUR APPLICATION AND NOT JUST USE IT AS IS!!

Simple C# bot using the superb ExchangeSharp, Skender-TechnicalAnalysis and MaterialDesign WPF packages.

This is just a a 2-day hackathon to build an app to monitor the top 20 coins on Binance-US and to notifiy me of the current MACD trends so that it would alert me to take a closer look.  This is just a play app and not really expecting any financial results from this but it just saves me time from having to log into my account to look at the TradingView charts.

![screenshot](/assets/binance-crypto-bot.PNG)

## Some stretch goals
1. I just didn't want to spend more than a day or 2 on this bot but it would have been nicer to have written this with .NET core so that I can run it on a Raspberry PI and a web interface to look at the charts but that is for another day.
2. I would like to add in email notification when there is a buy signal at some point.
3. I am experimenting with the bot so I am just looking at what other useful features I can add to it. 

## Updates
I did spend a few more nights adding in live trading for my very specific application.  It is now able to automatically buy when the criteria is hit but I don't really have a good sell criteria so I just added a button for manual sell when i feel like it.  A lot of things are hardcoded like the principal per coin and some other criteria.  Currently it only works properly for 8-hour candle.

# Disclaimer
Please do your own due diligence on any investments.  This application is not intended to replace any financial advice from real professionals.  I am doing this purely for the software fun of it and used solely for my personal use.  I also don't provide any guarantees that the the data is accurate and bug-free.  
