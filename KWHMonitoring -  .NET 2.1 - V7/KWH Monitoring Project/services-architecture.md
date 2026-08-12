---
name: Services Architecture
description: Background services and their implementations
type: project
---

# KWH Monitoring Project - Services Architecture

## Services Architecture

### EnergyAggregationBackgroundService

#### Responsibilities
1. **Periodic Execution**: Runs every minute to check for aggregation opportunities
2. **Thread Safety**: Uses semaphore to prevent concurrent execution
3. **Smart Timing**: Only aggregates when appropriate (e.g., at the start of each hour/day/month/year)
4. **Error Handling**: Graceful error recovery with logging

#### Key Methods
1. **ExecuteAsync**: Main service execution loop
2. **TryAggregateAsync**: Attempts to perform aggregation
3. **AggregateHourlyAsync**: Performs hourly data aggregation
4. **AggregateDailyAsync**: Performs daily data aggregation
5. **AggregateMonthlyAsync**: Performs monthly data aggregation
6. **AggregateYearlyAsync**: Performs yearly data aggregation
7. **BackfillAllAsync**: Processes historical data for initial population

#### Performance Considerations
- Batch processing to reduce database round trips
- Parallel processing for different devices
- Async/await patterns for I/O-bound operations
- Semaphore-based locking for thread safety

### AnomalyNotificationBackgroundService

#### Responsibilities
1. **Continuous Monitoring**: Constantly checks for abnormal conditions
2. **Threshold Detection**: Compares current readings against defined thresholds
3. **Log Generation**: Records all anomaly detections
4. **Notification Triggering**: Initiates notification processes when anomalies occur

#### Anomaly Detection Logic
1. **High Load Detection**: >20,000W power consumption
2. **Medium Load Detection**: >10,000W power consumption
3. **Normal Load**: ≤10,000W power consumption
4. **Voltage/Current Issues**: Out-of-range measurements

### NotificationService

#### Responsibilities
1. **Notification Delivery**: Sends alerts via configured channels
2. **Queue Management**: Manages notification queue
3. **Configuration Handling**: Reads notification settings
4. **Error Recovery**: Handles delivery failures gracefully