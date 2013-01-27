﻿using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;

// ReSharper disable CheckNamespace
namespace System.Reactive.Linq
// ReSharper restore CheckNamespace
{
    public static class ObservableFx
    {
        public static IObservable<T> Retry<T>(this IObservable<T> source, int retryCount, TimeSpan delaySpan)
        {
            return source.Retry<T, Exception>(retryCount, null, delaySpan, TaskPoolScheduler.Default);
        }

        public static IObservable<T> Retry<T, TException>(this IObservable<T> source, int retryCount, Action<TException> exAction, TimeSpan delaySpan)
            where TException : Exception
        {
            return source.Retry(retryCount, exAction, delaySpan, TaskPoolScheduler.Default);
        }

        public static IObservable<T> Retry<T, TException>(this IObservable<T> source, int retryCount, Action<TException> exAction, TimeSpan delaySpan, IScheduler scheduler)
            where TException : Exception
        {
            return source.Catch((TException ex) =>
            {
                if (exAction != null)
                {
                    exAction(ex);
                }

                //リトライ回数1回の場合はリトライしない
                if (retryCount == 1)
                {
                    return Observable.Throw<T>(ex);
                }

                //リトライ回数0(一応0未満)の場合無限リトライ
                if (retryCount <= 0)
                {
                    return Observable.Timer(delaySpan, scheduler).SelectMany(_ => source.Retry(retryCount, exAction, delaySpan, scheduler));
                }

                return
                    Observable.Timer(delaySpan, scheduler)
                              .SelectMany(_ => source.Retry(retryCount, exAction, delaySpan, scheduler, 1));
            });
        }

        private static IObservable<T> Retry<T, TException>(this IObservable<T> source, int retryCount, Action<TException> exAction, TimeSpan delaySpan, IScheduler scheduler, int nowRetryCount)
            where TException : Exception
        {
            return source.Catch((TException ex) =>
            {
                nowRetryCount++;

                if (exAction != null)
                {
                    exAction(ex);
                }

                if (nowRetryCount < retryCount)
                {
                    return Observable.Timer(delaySpan, scheduler).SelectMany(_ => source.Retry(retryCount, exAction, delaySpan, scheduler, nowRetryCount));
                }

                return Observable.Throw<T>(ex);
            });
        }

        public static IObservable<T> While<T>(this IObservable<T> source, Func<bool> condition)
        {
            return WhileCore(condition, source).Concat();
        }

        public static IObservable<T> DoWhile<T>(this IObservable<T> source, Func<bool> condition)
        {
            return source.Concat(source.While(condition));
        }

        private static IEnumerable<IObservable<T>> WhileCore<T>(Func<bool> condition, IObservable<T> source)
        {
            while (condition())
            {
                yield return source;
            }
        }

        public static IObservable<T> ConcatIfEmpty<T>(this IObservable<T> source, Func<IObservable<T>> next)
        {
            return source
                .Materialize()
                .Select((n, i) => (n.Kind == NotificationKind.OnCompleted && i == 0) ?
                    next().Materialize() : Observable.Return(n))
                .SelectMany(ns => ns)
                .Dematerialize();
        }

        public static IObservable<T> OrderBy<T, TKey>(this IObservable<T> observable,
            Func<T, TKey> keySelector)
        {
            var material = new List<Notification<T>>();
            return observable.Materialize()
                .Select(_ =>
                {
                    switch (_.Kind)
                    {
                        case NotificationKind.OnError:
                            return EnumerableEx.Return(_);
                        case NotificationKind.OnNext:
                            material.Add(_);
                            break;
                        case NotificationKind.OnCompleted:
                            return material.OrderBy(i => keySelector(i.Value))
                                .Concat(EnumerableEx.Return(_));
                    }
                    return Enumerable.Empty<Notification<T>>();
                })
                .SelectMany(_ => _)
                .Dematerialize();
        }

        public static IObservable<T> OrderByDescending<T, TKey>(this IObservable<T> observable,
            Func<T, TKey> keySelector)
        {
            var material = new List<Notification<T>>();
            return observable.Materialize()
                .Select(_ =>
                {
                    switch (_.Kind)
                    {
                        case NotificationKind.OnError:
                            return EnumerableEx.Return(_);
                        case NotificationKind.OnNext:
                            material.Add(_);
                            break;
                        case NotificationKind.OnCompleted:
                            return material.OrderByDescending(i => keySelector(i.Value))
                                .Concat(EnumerableEx.Return(_));
                    }
                    return Enumerable.Empty<Notification<T>>();
                })
                .SelectMany(_ => _)
                .Dematerialize();
        }
    }
}
