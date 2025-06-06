import 'chartjs-plugin-streaming';
import * as React from "react";
import { Bar } from 'react-chartjs-2';

import { InteractionModeMap } from 'react-chartjs-2';

export type DataSummaryPoint = {
	x: string;	// time
	y: number;	// value
	key: any;
}

type Props = {
	data: DataSummaryPoint[];
	summationWindow: number;
	meterId: string;
};

export const DataSummationChart = (class DataSummationChartComponent extends React.Component<Props> {

	constructor(props) {
		super(props);
	}

	render() {
		const data = {
			datasets: [
				{
					fill: false,
					label: this.props.meterId,
					borderColor: 'rgb(255, 99, 132)',
					backgroundColor: 'rgba(255, 99, 132, 0.25)',
					lineTension: 0,
					frameRate: 10,
					borderWidth: 0.7,
					pointBorderWidth: 0.5,
					pointRadius: 1,
					data: this.props.data
				}
			]
		};

		const options = {

			title: {
				display: true,
			},
			dataset: {
				barThickness: 15,
			},
			scales: {
				xAxis: {
					type: 'time',
					realtime: {
						duration: this.props.summationWindow * 60 * 1000,
						delay: 2000,
					},
					distribution: 'linear',
				},
				yAxis: {
					scaleLabel: {
						display: true,
						labelString: 'US Gal'
					},
					ticks: {
						beginAtZero: true,
					},
				}
			},
			tooltips: {
				mode: InteractionModeMap.nearest,
				intersect: false
			},
			hover: {
				mode: InteractionModeMap.nearest,
				intersect: false
			},
			maintainAspectRatio: false
		}

		return (
			<Bar data={data} options={options} />
		);
	}
});
