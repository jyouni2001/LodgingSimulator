using System;
using System.Diagnostics;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    public GameObject testObject;
    private int iterations = 1000000;

    void Start()
    {
        testObject = new GameObject();

        // == null 체크 전 메모리 측정
        long memoryBefore = GC.GetTotalMemory(false);

        // == null 체크
        Stopwatch sw1 = new Stopwatch();
        sw1.Start();
        for (int i = 0; i < iterations; i++)
        {
            if (testObject == null)
            {
                // do nothing
            }
        }
        sw1.Stop();

        // == null 체크 후 메모리 측정
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed1 = memoryAfter - memoryBefore;

        // is null 체크 전 메모리 측정
        memoryBefore = GC.GetTotalMemory(false);

        // is null 체크
        Stopwatch sw2 = new Stopwatch();
        sw2.Start();
        for (int i = 0; i < iterations; i++)
        {
            if (testObject is null)
            {
                // do nothing
            }
        }
        sw2.Stop();

        // is null 체크 후 메모리 측정
        memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed2 = memoryAfter - memoryBefore;

        UnityEngine.Debug.Log($"== null check time: {sw1.ElapsedMilliseconds} ms, ticks: {sw1.ElapsedTicks} ticks, memory used: {memoryUsed1} bytes");
        UnityEngine.Debug.Log($"is null check time: {sw2.ElapsedMilliseconds} ms, ticks: {sw2.ElapsedTicks} ticks, memory used: {memoryUsed2} bytes");
    }
}
