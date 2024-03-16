import pandas as pd
from prophet import Prophet
import json

def forecast_data(dates, revenues, period):
    df = pd.DataFrame({'ds': dates, 'y': revenues})
    df['ds'] = pd.to_datetime(df['ds'])  # Convert 'ds' column to datetimelike values
    m = Prophet()
    m.fit(df)
    future = m.make_future_dataframe(periods=period)
    forecast = m.predict(future)
    forecast['ds'] = pd.to_datetime(forecast['ds'])  # Convert 'ds' column to datetimelike values
    forecasts = forecast[['ds', 'yhat', 'yhat_lower', 'yhat_upper']].to_dict('records')
    
    return json.dumps(forecasts, default = str)