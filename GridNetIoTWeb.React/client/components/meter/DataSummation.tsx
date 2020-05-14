import { pink } from "@material-ui/core/colors";
import MenuItem from '@material-ui/core/MenuItem';
import Paper from "@material-ui/core/Paper";
import Select from '@material-ui/core/Select';
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import * as React from "react";
import { DataSummaryPoint, DataSummationChart } from "./DataSummationChart";


const styles = (theme: Theme) => createStyles({
	div: {
		marginLeft: "auto",
		marginRight: "auto",
		width: "95%",
	},
	header: {
		color: "white",
		backgroundColor: pink[500],
		padding: 10,
		fontSize: 24,
	},
	selectLabel: { color: pink[400] },
	chart: {
		height: 400,
	}
});

interface Props extends WithStyles<typeof styles> {
	data: DataSummaryPoint[];
	summationWindow: number;
	onWindowChange: any;
	meterId: string;
};

export default withStyles(styles)(class DataSummationComponent extends React.Component<Props, any> {

	constructor(props: Props) {
		super(props);
	}

	render() {
		const summationWindows = [
			{
				Id: 1,
				Value: "Last minute"
			},
			{
				Id: (1 * 24 * 60),
				Value: "Last 24 hours"
			},
			{
				Id: (3 * 24 * 60),
				Value: "Last 3 days"
			},
			{
				Id: (7 * 24 * 60),
				Value: "Last 7 days"
			},
			{
				Id: (30 * 24 * 60),
				Value: "Last 30 days"
			}
		];

		return (
			<Paper>
				<div className={this.props.classes.header}>Water Use</div>
				<div className={this.props.classes.div}>
					<form>

						<Select
							value={this.props.summationWindow}
							onChange={this.props.onWindowChange}
							inputProps={{
								name: 'age',
								id: 'age-simple',
							}}
						>
							<MenuItem value="">
								<em>None</em>
							</MenuItem>
							{summationWindows.map(item => <MenuItem key={item.Id} value={item.Id}>{item.Value}</MenuItem>)}
						</Select>
						{/*
						<SelectField value={this.props.summationWindow} onChange={this.props.onWindowChange} floatingLabelText="Show Water Use for the" floatingLabelStyle={styles.selectLabel}></SelectField>
						*/}
					</form>
					<div className={this.props.classes.chart}>
						<DataSummationChart data={this.props.data} summationWindow={this.props.summationWindow} meterId={this.props.meterId} />
					</div>
				</div>
			</Paper>
		);
	}
});
