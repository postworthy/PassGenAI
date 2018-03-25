__kernel void
walk(__global int* tblNgram, __global int* globalList, int ngi, int longestChain, int length)
{
	int groupItemIndex = get_local_id(0);
	
	for (int pass = 0; pass < length; pass++)
	{
		int groupSize = (int)(totalElements / pow(longestChain, pass));
		int groupCount = totalElements / groupSize;
		int group = groupItemIndex / groupSize;
		int globalOffset = (groupItemIndex * length) + pass;
		int ngl = 0;
		if (pass != 0)
		{
			for (int i = 0; i < tblNgram.Length; i++)
			{
				if (tblNgram[i][0] == globalList[globalOffset - 1])
					ngl = i;
			}
		}
		int index = group < longestChain ? group : (int)((((group * 1.0) / longestChain) - trunc((group * 1.0) / longestChain)) * longestChain);
		globalList[globalOffset] = tblNgram[((pass == 0 ? ngi : ngl)*longestChain) + (pass == 0 ? 0 : index)];
	}
}