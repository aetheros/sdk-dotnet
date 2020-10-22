#!/bin/bash

set -e

#cd AetherosOneM2MSDK/Aetheros.Schema.OneM2M
cp Aetheros.Schema.OneM2M.cs.bak Aetheros.Schema.OneM2M.cs

echo "namespace Aetheros.Schema.OneM2M {" > DefaultPrimitiveContent.cs
for i in Notification AggregatedNotification NotificationNotificationEvent AggregatedRequest AggregatedRequestRequest AggregatedResponse CSEBase Delivery OperationResult PrimitiveContent Request RequestPrimitive ResponseContent ResponsePrimitive
do

	if [ $i != "PrimitiveContent" ]
	then
		sed -i -E -e "s#( : ([A-Za-z0-9_.]*\.)?$i\b)#<TPrimitiveContent> : TPrimitiveContent where TPrimitiveContent : PrimitiveContent#" Aetheros.Schema.OneM2M.cs
		echo "	public partial class $i : $i<PrimitiveContent> {}" >> DefaultPrimitiveContent.cs
		sed -i -E -e "s#(public( partial)? class $i\b)( : \S*)?#\1<TPrimitiveContent>\3 where TPrimitiveContent : PrimitiveContent#" Aetheros.Schema.OneM2M.cs
		sed -i -E -e "s#(\spublic ([A-Za-z0-9_.]*\.)?$i\b)#\1<TPrimitiveContent>#" Aetheros.Schema.OneM2M.cs
	else
		sed -i -E -e "s#(\spublic ([A-Za-z0-9_.]*\.)?)$i\b#public TPrimitiveContent#" Aetheros.Schema.OneM2M.cs
	fi

	sed -i -E -e "s#(Empty|ICollection)<(([A-Za-z0-9_.]*\.)?$i)>#\1<\2<TPrimitiveContent>>#g" Aetheros.Schema.OneM2M.cs


done

echo '}' >> DefaultPrimitiveContent.cs